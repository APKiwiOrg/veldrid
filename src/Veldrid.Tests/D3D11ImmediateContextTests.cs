using System;
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
