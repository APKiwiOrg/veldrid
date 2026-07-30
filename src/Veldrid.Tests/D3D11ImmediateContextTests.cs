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
#endif
}