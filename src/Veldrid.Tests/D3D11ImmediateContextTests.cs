using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Veldrid.Tests
{
#if TEST_D3D11
    [Trait("Backend", "D3D11ImmediateContext")]
    public class D3D11ImmediateContextBufferTests : BufferTestBase<D3D11ImmediateContextDeviceCreator> { }

    [Trait("Backend", "D3D11ImmediateContext")]
    public class D3D11ImmediateContextRenderTests : RenderTests<D3D11ImmediateContextDeviceCreator> { }

    [Trait("Backend", "D3D11ImmediateContext")]
    public class D3D11ImmediateContextResourceSetTests : ResourceSetTests<D3D11ImmediateContextDeviceCreator> { }

    [Trait("Backend", "D3D11ImmediateContext")]
    public class D3D11ImmediateContextTextureTests : TextureTestBase<D3D11ImmediateContextDeviceCreator> { }

    [Trait("Backend", "D3D11")]
    public class D3D11PipelineSwitchTests : D3D11PipelineSwitchTestBase<D3D11DeviceCreator> { }

    [Trait("Backend", "D3D11ImmediateContext")]
    public class D3D11ImmediateContextPipelineSwitchTests : D3D11PipelineSwitchTestBase<D3D11ImmediateContextDeviceCreator> { }

    // D3D11 specific on purpose, and not a portability claim. The D3D11 backend leaves the device holding a
    // resource set across a pipeline switch, so a set bound under one pipeline is still in effect under the
    // next one when the two share their layouts. Other backends do not promise that, and Vulkan faults on
    // this sequence, so the test is not lifted into the shared per-backend suites. It is here because the
    // backend batches its resource set fan-out to the next draw or dispatch, which puts a pipeline switch
    // between a bind and its flush, and that is the one place the batching could drop a binding outright.
    public abstract class D3D11PipelineSwitchTestBase<T> : GraphicsDeviceTestBase<T> where T : GraphicsDeviceCreator
    {
        private const uint ValueCount = 64;
        private const uint Sentinel = 0xDEADBEEF;

        [Fact]
        public void ComputeResourceSetBoundBeforeAPipelineSwitchSurvivesIt()
        {
            uint sizeInBytes = ValueCount * sizeof(uint);

            DeviceBuffer copySrc = RF.CreateBuffer(
                new BufferDescription(sizeInBytes, BufferUsage.StructuredBufferReadOnly, sizeof(uint), true));
            DeviceBuffer copyDst = RF.CreateBuffer(
                new BufferDescription(sizeInBytes, BufferUsage.StructuredBufferReadWrite, sizeof(uint), true));

            ResourceLayout layout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("CopySrc", ResourceKind.StructuredBufferReadOnly, ShaderStages.Compute),
                new ResourceLayoutElementDescription("CopyDst", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute)));

            ResourceSet set = RF.CreateResourceSet(new ResourceSetDescription(layout, copySrc, copyDst));

            // Two distinct pipelines over the same shader and the same layout, which is what a real renderer
            // ends up with when two pipelines differ only in fixed-function state.
            Pipeline first = RF.CreateComputePipeline(new ComputePipelineDescription(
                TestShaders.LoadCompute(RF, "FillBuffer"), layout, 1, 1, 1));
            Pipeline second = RF.CreateComputePipeline(new ComputePipelineDescription(
                TestShaders.LoadCompute(RF, "FillBuffer"), layout, 1, 1, 1));

            uint[] srcData = new uint[ValueCount];
            uint[] dstData = new uint[ValueCount];
            for (uint i = 0; i < ValueCount; i++)
            {
                srcData[i] = i + 1;
                dstData[i] = Sentinel;
            }
            GD.UpdateBuffer(copySrc, 0, srcData);
            GD.UpdateBuffer(copyDst, 0, dstData);

            CommandList cl = RF.CreateCommandList();
            cl.Begin();

            // Bind under the first pipeline, dispatch under the second, with no rebind in between. The set
            // has to reach the device before the switch drops its record, or the dispatch runs with nothing
            // bound and leaves the sentinel in place.
            cl.SetPipeline(first);
            cl.SetComputeResourceSet(0, set);
            cl.SetPipeline(second);
            cl.Dispatch(ValueCount, 1, 1);

            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            DeviceBuffer readback = GetReadback(copyDst);
            MappedResourceView<uint> readView = GD.Map<uint>(readback, MapMode.Read);
            for (uint i = 0; i < ValueCount; i++)
            {
                Assert.Equal(srcData[i], readView[i]);
            }
            GD.Unmap(readback);
        }

        // A throw guard, so completing without an exception is the assertion. The sequence walks the backend
        // into a state where a slot is marked dirty while its record has already been cleared, which the
        // draining pipeline switch would otherwise try to activate.
        [Fact]
        public void PipelineSwitchAfterAFramebufferUnbindsTheSampledTarget()
        {
            Texture target = RF.CreateTexture(TextureDescription.Texture2D(
                32, 32, 1, 1, PixelFormat.B8_G8_R8_A8_UNorm, TextureUsage.Sampled | TextureUsage.RenderTarget));
            Framebuffer targetFramebuffer = RF.CreateFramebuffer(new FramebufferDescription(null, target));

            ResourceLayout layout = RF.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Tex", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Smp", ResourceKind.Sampler, ShaderStages.Fragment)));

            ResourceSet samplingSet = RF.CreateResourceSet(new ResourceSetDescription(layout, target, GD.PointSampler));

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                Array.Empty<VertexLayoutDescription>(),
                TestShaders.LoadVertexFragment(RF, "FullScreenTriSampleTexture2D"));

            // Two pipelines over the same shaders and the same layout, differing only in blend state, which
            // is the pairing a real renderer produces and the one where a set stays valid across the switch.
            GraphicsPipelineDescription gpd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleList,
                shaderSet,
                layout,
                new OutputDescription(null, new OutputAttachmentDescription(PixelFormat.B8_G8_R8_A8_UNorm)));
            Pipeline first = RF.CreateGraphicsPipeline(ref gpd);

            gpd.BlendState = BlendStateDescription.SingleAlphaBlend;
            Pipeline second = RF.CreateGraphicsPipeline(ref gpd);

            CommandList cl = RF.CreateCommandList();
            cl.Begin();

            // Bind the set, then switch. The drain fans the set out, which is what records the texture as
            // bound at slot 0, and the switch then clears the slot 0 record while that texture record lives on.
            cl.SetPipeline(first);
            cl.SetGraphicsResourceSet(0, samplingSet);
            cl.SetPipeline(second);

            // Binding the same texture as a colour target unbinds it as an SRV, which marks slot 0 dirty by
            // the surviving texture record, even though slot 0 no longer holds a set.
            cl.SetFramebuffer(targetFramebuffer);

            // The drain here meets that mark on a cleared record.
            cl.SetPipeline(first);

            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();
        }
    }

    [Trait("Backend", "D3D11ImmediateContext")]
    public class D3D11ImmediateContextRecordingTests : GraphicsDeviceTestBase<D3D11ImmediateContextDeviceCreator>
    {
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

        [Fact]
        public void DoubleBeginThrowsAndDoesNotLeakTheRecordingLock()
        {
            CommandList cl = RF.CreateCommandList();

            cl.Begin();
            Assert.Throws<VeldridException>(() => cl.Begin());
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            AssertImmediateContextIsFree();
        }

        [Fact]
        public void BeginAfterEndWithoutSubmitDoesNotLeakTheRecordingLock()
        {
            CommandList cl = RF.CreateCommandList();

            // Legal per the CommandList contract: Begin is valid once End has been called. In immediate
            // mode the second Begin must not take the recording lock again, because only the one Exit at
            // SubmitCommands follows it.
            cl.Begin();
            cl.End();
            cl.Begin();
            cl.End();
            GD.SubmitCommands(cl);
            GD.WaitForIdle();

            AssertImmediateContextIsFree();
        }

        [Fact]
        public void DeviceCallOnAnotherThreadDuringRecordingBlocksRatherThanDeadlocks()
        {
            CommandList cl = RF.CreateCommandList();

            // A partial update of a dynamic buffer is neither UpdateSubresource nor a full WriteDiscard map,
            // so it routes through D3D11CommandList's staging-upload branch. That branch re-enters the device
            // and reaches the mapped-resource lock while this thread already holds the recording lock.
            DeviceBuffer target = RF.CreateBuffer(
                new BufferDescription(64, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
            DeviceBuffer probe = RF.CreateBuffer(new BufferDescription(64, BufferUsage.Staging));

            using ManualResetEventSlim recordingStarted = new ManualResetEventSlim();
            using ManualResetEventSlim workerStarted = new ManualResetEventSlim();

            // Both sides run off the test thread on purpose. If the ordering regresses, the assert below
            // fails instead of wedging the test thread and hanging the whole run.
            Task render = Task.Run(() =>
            {
                cl.Begin();
                recordingStarted.Set();
                workerStarted.Wait();

                // Give the worker time to get into its device call and take whatever it takes on the way in.
                Thread.Sleep(250);
                cl.UpdateBuffer(target, 16, new byte[16]);

                cl.End();
                GD.SubmitCommands(cl);
            });

            recordingStarted.Wait();

            Task worker = Task.Run(() =>
            {
                workerStarted.Set();
                GD.Map(probe, MapMode.Write);
                GD.Unmap(probe);
            });

            Assert.True(
                Task.WaitAll(new[] { render, worker }, TimeSpan.FromSeconds(20)),
                "A worker thread calling into the device deadlocked against the recording thread. The "
                + "recording lock has to be the outermost lock in the backend, so that a worker can never "
                + "hold an inner lock while the recording thread goes on to wait for that same inner lock.");

            GD.WaitForIdle();
        }

        // The recording lock is reentrant, so probing it on the recording thread would succeed even with a
        // leaked recursion. Probe from another thread, with a timeout, so a leak fails rather than hangs.
        private void AssertImmediateContextIsFree()
        {
            DeviceBuffer buffer = RF.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            Task update = Task.Run(() => GD.UpdateBuffer(buffer, 0, new byte[16]));

            Assert.True(
                update.Wait(ProbeTimeout),
                "A device-level UpdateBuffer from another thread is still blocked on the immediate-context "
                + "recording lock, so the lock was left held after SubmitCommands.");
        }
    }
#endif
}
