//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Runtime.CompilerServices;

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Provides methods to query and control In-Field Update (IFU) state, per image (nanoCLR or
    /// deployment). All methods are static; the class cannot be instantiated.
    /// </summary>
    /// <remarks>
    /// This API is optional. On devices where updates are performed entirely through another,
    /// host-driven mechanism, <see cref="UpdateManager"/> is not required.
    /// </remarks>
    public static class UpdateManager
    {

#region Query

        /// <summary>
        /// Returns the current update state of the given image.
        /// </summary>
        /// <param name="image">Which image to query (nanoCLR or deployment).</param>
        /// <returns>One of the <see cref="UpdateStatus"/> values.</returns>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UpdateStatus GetStatus(ImageType image);

        /// <summary>
        /// Returns metadata about the primary (active) slot of the given image.
        /// </summary>
        /// <param name="image">Which image to query.</param>
        /// <returns>
        /// An <see cref="ImageInfo"/> instance, or <c>null</c> if no valid image is found.
        /// </returns>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern ImageInfo GetPrimaryImageInfo(ImageType image);

        /// <summary>
        /// Returns metadata about the secondary (staging) slot of the given image.
        /// </summary>
        /// <param name="image">Which image to query.</param>
        /// <returns>
        /// An <see cref="ImageInfo"/> instance, or <c>null</c> if the secondary slot is empty or
        /// invalid.
        /// </returns>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern ImageInfo GetSecondaryImageInfo(ImageType image);

        /// <summary>
        /// Returns metadata for every image/slot on the device (both slots of every image). Each
        /// entry identifies itself via <see cref="ImageInfo.Image"/> and <see cref="ImageInfo.Slot"/>.
        /// </summary>
        /// <returns>An array with one entry per image/slot combination.</returns>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern ImageInfo[] GetImageList();

        #endregion

        #region Write / stage

        /// <summary>
        /// Erases the entire secondary (staging) slot of the given image.
        /// </summary>
        /// <param name="image">Which image's secondary slot to erase.</param>
        /// <returns>
        /// <see cref="UpdateSessionResult.Success"/> when the slot was erased;
        /// <see cref="UpdateSessionResult.Busy"/> while an update session is open on the image by any
        /// writer; <see cref="UpdateSessionResult.NoSlot"/> when the image has no secondary slot on
        /// this target; <see cref="UpdateSessionResult.FlashError"/> when the erase failed.
        /// </returns>
        /// <remarks>
        /// <b>Destructive.</b> This erases the whole secondary slot of the given image. Any update
        /// image previously staged there is permanently lost and cannot be recovered. Only call this
        /// to deliberately discard a staged update;
        /// <see cref="StartUpdateSession(ImageType, int, out UpdateSession)"/> erases the slot itself.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UpdateSessionResult EraseSecondaryImage(ImageType image);

        #endregion

        #region Update sessions

        /// <summary>
        /// Opens an update session for the given image: claims the secondary (staging) slot for this
        /// writer, erases it and hands back a handle that authorises writing the image in chunks.
        /// </summary>
        /// <param name="image">Which image will be staged.</param>
        /// <param name="totalLength">
        /// Total length of the image to stage (MCUboot header, payload and TLV area) - typically the
        /// download's content length.
        /// </param>
        /// <param name="session">
        /// When this method returns <see cref="UpdateSessionResult.Success"/>, the open session;
        /// otherwise, <c>null</c>.
        /// </param>
        /// <returns>
        /// <see cref="UpdateSessionResult.Success"/> when the session is open;
        /// <see cref="UpdateSessionResult.Busy"/> when another writer holds a session on the image;
        /// <see cref="UpdateSessionResult.SwapInFlight"/> when a swap is already scheduled;
        /// <see cref="UpdateSessionResult.TooLarge"/> when the image does not fit the slot;
        /// <see cref="UpdateSessionResult.NoSlot"/> when the image has no secondary slot on this target;
        /// <see cref="UpdateSessionResult.FlashError"/> when the erase failed.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="totalLength"/> is not positive.</exception>
        /// <remarks>
        /// <para>
        /// Only one session can be open per image across managed code, the Wire Protocol and native
        /// code. Opening a session while this same writer already holds one replaces the previous
        /// session, so an abandoned handle can never wedge the image.
        /// </para>
        /// <para>
        /// Sessions live in RAM: every reboot releases them, including a CLR-only restart such as a
        /// deployment from Visual Studio. What was already written to the slot is not affected, so an
        /// interrupted download continues with
        /// <see cref="ResumeUpdateSession(ImageType, int, byte[], out UpdateSession)"/>.
        /// </para>
        /// <para>
        /// <b>Destructive:</b> the whole secondary slot is erased before this returns, which can take
        /// a few seconds on large external flash. Any image previously staged there is lost. To
        /// continue a download that was interrupted, use
        /// <see cref="ResumeUpdateSession(ImageType, int, byte[], out UpdateSession)"/> instead.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UpdateSessionResult StartUpdateSession(
            ImageType image,
            int totalLength,
            out UpdateSession session);

        /// <summary>
        /// Reopens an update session on an image that was partially stored in the secondary slot by
        /// an earlier session - for example one interrupted by a reboot or a lost connection - so the
        /// download can continue from <see cref="UpdateSession.NextOffset"/> instead of starting over.
        /// </summary>
        /// <param name="image">Which image is being staged.</param>
        /// <param name="totalLength">
        /// Total length of the image, as for <see cref="StartUpdateSession(ImageType, int, out UpdateSession)"/>.
        /// </param>
        /// <param name="expectedHeader">
        /// The first bytes of the image being downloaded (up to 32, the MCUboot header), or
        /// <c>null</c> to skip the check. Strongly recommended: after a completed swap the secondary
        /// slot holds the <i>previous</i> image with a perfectly valid header, and only the caller can
        /// tell that apart from a paused download. A 32-byte HTTP range request (<c>bytes=0-31</c>)
        /// is enough to obtain it.
        /// </param>
        /// <param name="session">
        /// When this method returns <see cref="UpdateSessionResult.Success"/>, the open session,
        /// positioned at the offset to continue from; otherwise, <c>null</c>.
        /// </param>
        /// <returns>
        /// <see cref="UpdateSessionResult.Success"/> when the session is open;
        /// <see cref="UpdateSessionResult.NoImage"/> when the slot is erased or too little was stored,
        /// and <see cref="UpdateSessionResult.HeaderMismatch"/> when a different image is stored -
        /// start a fresh session in both cases; <see cref="UpdateSessionResult.Busy"/>,
        /// <see cref="UpdateSessionResult.SwapInFlight"/>, <see cref="UpdateSessionResult.TooLarge"/>,
        /// <see cref="UpdateSessionResult.NoSlot"/> or <see cref="UpdateSessionResult.FlashError"/> as
        /// for <see cref="StartUpdateSession(ImageType, int, out UpdateSession)"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="totalLength"/> is not positive.</exception>
        /// <remarks>
        /// <para>
        /// No bookkeeping is kept for this: the secondary slot itself is the state. The runtime reads
        /// the MCUboot header already stored, finds how far the previous session got (the last byte
        /// that is not erased flash), rewinds to the start of the erase block containing that point
        /// and erases that block again - a block that was being programmed when power failed cannot
        /// be repaired by rewriting it. The returned <see cref="UpdateSession.NextOffset"/> is
        /// therefore at most the amount previously stored, and the caller re-sends from there.
        /// </para>
        /// <para>
        /// If the whole image had already been stored, <see cref="UpdateSession.NextOffset"/> equals
        /// <see cref="UpdateSession.TotalLength"/> and the caller can go straight to
        /// <see cref="CompleteUpdateSession(UpdateSession)"/>.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UpdateSessionResult ResumeUpdateSession(
            ImageType image,
            int totalLength,
            byte[] expectedHeader,
            out UpdateSession session);

        /// <summary>
        /// Appends a chunk of image data to the image being staged by <paramref name="session"/>.
        /// </summary>
        /// <param name="session">
        /// The open session, from <see cref="StartUpdateSession(ImageType, int, out UpdateSession)"/> or
        /// <see cref="ResumeUpdateSession(ImageType, int, byte[], out UpdateSession)"/>.
        /// </param>
        /// <param name="data">Buffer holding the chunk.</param>
        /// <param name="offset">Zero-based offset in <paramref name="data"/> at which the chunk begins.</param>
        /// <param name="count">Number of bytes to write from <paramref name="data"/>.</param>
        /// <returns>
        /// <see cref="UpdateSessionResult.Success"/> when the chunk was written;
        /// <see cref="UpdateSessionResult.BadMagic"/> when the first chunk of a fresh session does not
        /// start with a valid MCUboot header; <see cref="UpdateSessionResult.TooLarge"/> when the chunk
        /// runs past <see cref="UpdateSession.TotalLength"/>; <see cref="UpdateSessionResult.BadToken"/>
        /// for a stale or foreign session; <see cref="UpdateSessionResult.FlashError"/> when the write
        /// failed.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="data"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="count"/> is negative, or their sum is greater
        /// than the length of <paramref name="data"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <paramref name="offset"/> and <paramref name="count"/> follow the <c>Stream.Write</c>
        /// convention: they index into <paramref name="data"/>. The position in the image is not a
        /// parameter - every chunk is appended at <see cref="UpdateSession.NextOffset"/>, which the
        /// runtime advances by <paramref name="count"/> on success. Chunks are therefore sequential by
        /// construction and cannot interleave with another writer's.
        /// </para>
        /// <para>
        /// The first chunk of a fresh session must hold at least the 32-byte MCUboot header. A session
        /// released by a device or CLR restart reports <see cref="UpdateSessionResult.BadToken"/>. On
        /// any failure the position does not advance and the chunk may be retried.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UpdateSessionResult StoreImageChunk(UpdateSession session, byte[] data, int offset, int count);

        /// <summary>
        /// Verifies the staged image and, if it is intact, marks it pending so that MCUboot swaps it
        /// in on the next reboot as a revertible test image. Closes the session.
        /// </summary>
        /// <param name="session">The open session.</param>
        /// <returns>
        /// <see cref="UpdateSessionResult.Success"/> when the image is verified and pending;
        /// <see cref="UpdateSessionResult.Incomplete"/> when fewer than <see cref="UpdateSession.TotalLength"/>
        /// bytes have been stored (the session stays open and writable);
        /// <see cref="UpdateSessionResult.BadMagic"/>, <see cref="UpdateSessionResult.BadTlv"/> or
        /// <see cref="UpdateSessionResult.HashMismatch"/> when the staged image is not intact (the
        /// session is closed, the slot is left as is);
        /// <see cref="UpdateSessionResult.BadToken"/> for a stale or foreign session.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// <para>
        /// Verification is structural plus a SHA-256 of the image compared with the digest in its
        /// TLV area, so a corrupt or mismatched download is caught here, before any reboot. The
        /// signature is verified by MCUboot at boot time as usual.
        /// </para>
        /// <para>
        /// Typical flow for the deployment image: <c>CompleteUpdateSession</c> returns
        /// <see cref="UpdateSessionResult.Success"/>, the application calls <see cref="RequestReboot"/>,
        /// MCUboot swaps the image in, the new application validates itself and calls
        /// <see cref="ConfirmDeploymentImage"/>; if it does not, the previous image is restored on the
        /// following reboot.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UpdateSessionResult CompleteUpdateSession(UpdateSession session);

        /// <summary>
        /// Closes an update session without completing it.
        /// </summary>
        /// <param name="session">The open session.</param>
        /// <param name="eraseSlot">
        /// <see langword="true"/> to erase the secondary slot, discarding whatever was staged;
        /// <see langword="false"/> to leave the partial image in place so that
        /// <see cref="ResumeUpdateSession(ImageType, int, byte[], out UpdateSession)"/> can pick it up
        /// later (pause).
        /// </param>
        /// <returns>
        /// <see cref="UpdateSessionResult.Success"/> when the session was closed and, if requested, the
        /// slot erased; <see cref="UpdateSessionResult.BadToken"/> for a stale or foreign session;
        /// <see cref="UpdateSessionResult.FlashError"/> when the requested erase failed.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A session the runtime recognised is released whatever the erase did, so there is nothing
        /// to retry: calling this again only returns <see cref="UpdateSessionResult.BadToken"/>. When
        /// a requested erase failed the slot may still hold part of the image; discard it with
        /// <see cref="EraseSecondaryImage(ImageType)"/>, or simply open a fresh session, which erases
        /// the slot anyway.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UpdateSessionResult AbortUpdateSession(UpdateSession session, bool eraseSlot);

        /// <summary>
        /// Reports who currently holds an update session on the given image.
        /// </summary>
        /// <param name="image">Which image to query.</param>
        /// <returns>The owner, or <see cref="UpdateSessionOwner.None"/> when no session is open.</returns>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern UpdateSessionOwner GetUpdateSessionOwner(ImageType image);

        #endregion

        #region Confirm / revert

        /// <summary>
        /// Confirms (makes permanent) the <see cref="ImageType.Deployment"/> validation. If not called
        /// while the deployment image is in test state, the previous image is restored on the next reboot.
        /// </summary>
        /// <returns><see langword="true"/> if the image was confirmed; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool ConfirmDeploymentImage();

        /// <summary>
        /// Requests that the deployment image be reverted on the next reboot. Valid only while the
        /// deployment image is running in an unconfirmed test state.
        /// </summary>
        /// <returns><see langword="true"/> if the revert was scheduled; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool RequestDeploymentRevert();

        /// <summary>
        /// Requests a revert of the nanoCLR image by scheduling a swap of the image currently staged
        /// in its secondary slot on the next reboot.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> if the nanoCLR secondary slot does not hold a valid image to
        /// revert to; otherwise, <see langword="true"/>.
        /// </returns>
        /// <remarks>
        /// The running nanoCLR image is already confirmed at startup, so this cannot "un-confirm" it.
        /// It can only schedule a swap-in of a valid image that is already present in its secondary
        /// slot (for example, a copy of the previous firmware kept there). A reboot is required to
        /// complete the swap.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool RequestClrRevert();

        #endregion

        #region Reboot

        /// <summary>
        /// Reboots the device. Any swap that has been scheduled (test/permanent/revert) is applied
        /// during the reboot.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void RequestReboot();

        #endregion
    }
}
