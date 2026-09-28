//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Identifies who holds the update session currently open on an image.
    /// </summary>
    /// <remarks>
    /// As with <see cref="UpdateSessionResult"/>, the runtime tracks session ownership with these
    /// same values through the generated native assembly header, so changing or reordering them
    /// requires a matching firmware build.
    /// </remarks>
    public enum UpdateSessionOwner
    {
        /// <summary>
        /// No update session is open on the image.
        /// </summary>
        None = 0,

        /// <summary>
        /// A managed application opened the session through <see cref="UpdateManager"/>.
        /// </summary>
        Managed = 1,

        /// <summary>
        /// A host is writing the image over the Wire Protocol (debugger, nanoff).
        /// </summary>
        WireProtocol = 2,

        /// <summary>
        /// Native code on the device (for example an in-field update agent) opened the session.
        /// </summary>
        Native = 3,
    }
}
