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
