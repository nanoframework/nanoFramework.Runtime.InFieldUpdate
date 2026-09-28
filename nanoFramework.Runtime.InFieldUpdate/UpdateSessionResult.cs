//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Outcome of an update session operation. Returned by
    /// <see cref="UpdateManager.CompleteUpdateSession(UpdateSession)"/> and, for operations that
    /// report failure through a <c>null</c>/<see langword="false"/> return, readable via
    /// <see cref="UpdateManager.GetLastSessionError"/>.
    /// </summary>
    /// <remarks>
    /// This enum is the contract between both sides of the API: the interop generator emits it into
    /// the native assembly header, and the runtime's update session code reports its outcomes with
    /// these very values. Changing or reordering them requires a matching firmware build.
    /// </remarks>
    public enum UpdateSessionResult
    {
        /// <summary>
        /// The operation succeeded.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Another writer (managed, Wire Protocol or native) holds an update session for the image,
        /// or a flash operation is still in progress on it.
        /// </summary>
        Busy = 1,

        /// <summary>
        /// The session does not match the one currently open for the image (stale after
        /// <see cref="UpdateManager.CompleteUpdateSession(UpdateSession)"/> /
        /// <see cref="UpdateManager.AbortUpdateSession(UpdateSession, bool)"/>, or foreign).
        /// </summary>
        BadToken = 2,

        /// <summary>
        /// A chunk was offered at an offset other than the next expected one (Wire Protocol only;
        /// managed sessions are sequential by construction).
        /// </summary>
        BadOffset = 3,

        /// <summary>
        /// The image does not fit the usable size of the secondary slot, or a chunk runs past the
        /// declared total length.
        /// </summary>
        TooLarge = 4,

        /// <summary>
        /// A flash open, erase, write or read failed, or MCUboot refused to mark the image pending.
        /// </summary>
        FlashError = 5,

        /// <summary>
        /// The first chunk does not carry a valid MCUboot image header.
        /// </summary>
        BadMagic = 6,

        /// <summary>
        /// A swap is already scheduled or in progress for the image; stage after the next reboot.
        /// </summary>
        SwapInFlight = 7,

        /// <summary>
        /// Resume: the secondary slot holds no image header (erased, or the stored data was too
        /// short to keep). Start a fresh session instead.
        /// </summary>
        NoImage = 8,

        /// <summary>
        /// Resume: the stored header does not match the expected header bytes or the declared total
        /// length. The slot holds a different image.
        /// </summary>
        HeaderMismatch = 9,

        /// <summary>
        /// Complete: not every byte of the declared total length has been written yet. The session
        /// stays open.
        /// </summary>
        Incomplete = 10,

        /// <summary>
        /// Complete: the TLV area is missing, malformed or inconsistent with the total length.
        /// </summary>
        BadTlv = 11,

        /// <summary>
        /// Complete: the SHA-256 computed over the staged image does not match the digest in its TLV
        /// area. The staged data is corrupt or belongs to a different image.
        /// </summary>
        HashMismatch = 12,

        /// <summary>
        /// An argument was invalid.
        /// </summary>
        BadArgument = 13,

        /// <summary>
        /// The image has no secondary slot on this target.
        /// </summary>
        NoSlot = 14,
    }
}
