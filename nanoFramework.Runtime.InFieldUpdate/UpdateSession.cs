//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Handle to an open update session: the right to stage an image into the secondary slot of
    /// <see cref="Image"/>. Obtained from <see cref="UpdateManager.StartUpdateSession(ImageType, int, out UpdateSession)"/>
    /// or <see cref="UpdateManager.ResumeUpdateSession(ImageType, int, byte[], out UpdateSession)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only one session can be open per image, across every writer on the device (managed code,
    /// the Wire Protocol and native code). The session carries an opaque <see cref="Token"/> that
    /// the runtime checks on every chunk; the token gates writes, not the calling thread, so the
    /// instance may be handed to another thread but must never be used by two writers at once.
    /// </para>
    /// <para>
    /// Chunks are strictly sequential: every <see cref="UpdateManager.StoreImageChunk(UpdateSession, byte[], int, int)"/>
    /// appends at <see cref="NextOffset"/>, which the runtime advances. After
    /// <see cref="UpdateManager.CompleteUpdateSession(UpdateSession)"/> or
    /// <see cref="UpdateManager.AbortUpdateSession(UpdateSession, bool)"/> the instance is stale and
    /// any further use fails with <see cref="UpdateSessionResult.BadToken"/>.
    /// </para>
    /// <para>
    /// The handle does not outlive a reboot - the runtime releases every session when the CLR stops,
    /// so nothing stays claimed by an application that is gone. The bytes already staged in the slot
    /// do survive: <see cref="UpdateManager.ResumeUpdateSession(ImageType, int, byte[], out UpdateSession)"/> hands back
    /// a new session positioned where the previous one stopped.
    /// </para>
    /// </remarks>
    public class UpdateSession
    {
        /// <summary>
        /// Gets the image this session stages.
        /// </summary>
        public ImageType Image { get; }

        /// <summary>
        /// Gets the opaque token that authorises writes for this session. Not a secret: it exists
        /// to keep writers from interleaving, not to authenticate them.
        /// </summary>
        public uint Token { get; }

        /// <summary>
        /// Gets the total length of the image being staged (header, payload and TLV area), as
        /// declared when the session was opened.
        /// </summary>
        public int TotalLength { get; }

        /// <summary>
        /// Gets the image-relative offset the next chunk will be written at. Equals
        /// <see cref="TotalLength"/> once the whole image has been stored.
        /// </summary>
        public int NextOffset { get; }

        /// <summary>
        /// Gets a value indicating whether the session was reopened on a partially stored image
        /// (<see cref="UpdateManager.ResumeUpdateSession(ImageType, int, byte[], out UpdateSession)"/>) rather than
        /// started fresh.
        /// </summary>
        public bool IsResumed { get; }

        /// <summary>
        /// Gets the version from the MCUboot header of the staged image, or <c>null</c> while the
        /// header has not been seen yet (before the first chunk of a fresh session).
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Gets the MCUboot header size of the staged image, or 0 while the header is unknown.
        /// </summary>
        public int HeaderSize { get; }

        /// <summary>
        /// Gets the payload size declared by the MCUboot header of the staged image (bytes between
        /// the header and the TLV area), or 0 while the header is unknown.
        /// </summary>
        public int ImageSize { get; }

        /// <summary>
        /// Gets a value indicating whether every byte of <see cref="TotalLength"/> has been stored.
        /// </summary>
        public bool IsComplete => NextOffset >= TotalLength;

        /// <summary>
        /// Appends the whole of <paramref name="data"/> to the staged image.
        /// Equivalent to <c>UpdateManager.StoreImageChunk(this, data, 0, data.Length)</c>.
        /// </summary>
        /// <param name="data">Bytes to append.</param>
        /// <returns>
        /// The outcome, as for <see cref="UpdateManager.StoreImageChunk(UpdateSession, byte[], int, int)"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
        public UpdateSessionResult Write(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);

            return UpdateManager.StoreImageChunk(this, data, 0, data.Length);
        }

        // instances are created by the native runtime only
        internal UpdateSession()
        {
        }
    }
}
