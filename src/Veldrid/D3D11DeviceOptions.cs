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
        /// <remarks>
        /// <para>
        /// This is an opt-in workaround for drivers whose deferred-context path is slow or broken. Leaving it
        /// false keeps the stock deferred-context behaviour, which is what every other backend matches.
        /// </para>
        /// <para>
        /// Cost. The recording lock is taken at <see cref="CommandList.Begin"/> and released at
        /// <see cref="GraphicsDevice.SubmitCommands(CommandList)"/>, so it is held across a whole frame rather
        /// than around individual calls. Every other user of the immediate context waits for that whole span.
        /// That includes <see cref="GraphicsDevice.UpdateBuffer(DeviceBuffer, uint, IntPtr, uint)"/>,
        /// <see cref="GraphicsDevice.Map(MappableResource, MapMode)"/> and
        /// <see cref="GraphicsDevice.Unmap(MappableResource)"/> called from other threads, and
        /// <see cref="GraphicsDevice.SwapBuffers()"/>, which takes the same lock and holds it through a Present
        /// that blocks on vertical blank. Those other threads block, they do not deadlock: the recording lock
        /// is the outermost lock in the backend, so a worker cannot be holding something the recording thread
        /// goes on to want. What you lose is latency. A worker streaming uploads stalls until the frame in
        /// flight is submitted, and again for the length of each Present.
        /// </para>
        /// <para>
        /// Ordering. The lock is reentrant, so the recording thread is never blocked by its own frame. A
        /// device-level UpdateBuffer, Map or Unmap issued on that thread between
        /// <see cref="CommandList.Begin"/> and <see cref="GraphicsDevice.SubmitCommands(CommandList)"/>
        /// therefore executes at its natural position in the command stream. In deferred mode the same call
        /// runs on the immediate context while the recorded command list is still unexecuted, so it is visible
        /// to every draw in that list, including draws recorded before the call. In immediate mode it is
        /// visible only to draws recorded after it. Renderers that update a buffer mid-frame and expect the
        /// whole frame to see the new contents must record the update through
        /// <see cref="CommandList.UpdateBuffer(DeviceBuffer, uint, IntPtr, uint)"/>, which orders the same way
        /// in both modes.
        /// </para>
        /// <para>
        /// One recorder at a time. Every <see cref="CommandList"/> on the device records into the same
        /// immediate context, so only one of them can be open. <see cref="CommandList.Begin"/> on a second
        /// <see cref="CommandList"/> throws <see cref="VeldridException"/> while another one is open, rather
        /// than clearing the state that one has already bound. The open recorder is unaffected by the refusal
        /// and carries on, and the refused <see cref="CommandList"/> can begin normally once the open one has
        /// reached <see cref="GraphicsDevice.SubmitCommands(CommandList)"/>. Deferred mode has a context per
        /// <see cref="CommandList"/> and keeps allowing any number of them open at once.
        /// </para>
        /// <para>
        /// Resizing. <see cref="Swapchain.Resize(uint, uint)"/> runs on the immediate context and disposes the
        /// <see cref="Framebuffer"/> the recording thread has bound, so resize between frames, never during
        /// one. A resize during recording no longer corrupts the frame or throws from lock misuse, but the
        /// disposed framebuffer is still a hazard, exactly as it is in deferred mode.
        /// </para>
        /// </remarks>
        public bool UseImmediateContext;
    }
}
