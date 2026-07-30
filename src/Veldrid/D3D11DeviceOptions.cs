using System;

namespace Veldrid
{
    /// <summary>
    /// A structure describing Direct3D11-specific device creation options.
    /// </summary>
    public struct D3D11DeviceOptions
    {
        /// <summary>
        /// Native pointer to an adapter.
        /// </summary>
        public IntPtr AdapterPtr;

        /// <summary>
        /// Set of device specific flags.
        /// See <see cref="Vortice.Direct3D11.DeviceCreationFlags"/> for details.
        /// </summary>
        public uint DeviceCreationFlags;

        /// <summary>
        /// When true, command lists record directly into the Direct3D11 immediate context instead of a deferred
        /// context. Recording is serialized from <see cref="CommandList.Begin"/> through
        /// <see cref="GraphicsDevice.SubmitCommands(CommandList)"/>, and those calls must remain on the same thread.
        /// </summary>
        public bool UseImmediateContext;
    }
}
