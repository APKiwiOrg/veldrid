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
        /// context. Recording is serialized for the lifetime of each command list, so callers must not assume that
        /// separate command lists can record concurrently.
        /// </summary>
        public bool UseImmediateContext;
    }
}
