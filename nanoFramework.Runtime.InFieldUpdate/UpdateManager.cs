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
        //----------------------------------------------------------------------
        // Query
        //----------------------------------------------------------------------

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

        //----------------------------------------------------------------------
        // Write / stage
        //----------------------------------------------------------------------

        /// <summary>
        /// Erases the entire secondary (staging) slot of the given image.
        /// </summary>
        /// <param name="image">Which image's secondary slot to erase.</param>
        /// <returns><see langword="true"/> if the slot was erased; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// <b>Destructive.</b> This erases the whole secondary slot of the given image. Any update
        /// image previously staged there is permanently lost and cannot be recovered. Only call this
        /// when you are about to write a fresh image, or to deliberately discard a staged update.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool EraseSecondaryImage(ImageType image);

        /// <summary>
        /// Writes a chunk of image data into the secondary (staging) slot of the given image.
        /// </summary>
        /// <param name="image">Which image the chunk belongs to.</param>
        /// <param name="data">Buffer containing the image bytes.</param>
        /// <param name="offset">Byte offset within the secondary slot to begin writing.</param>
        /// <param name="length">Number of bytes to write from <paramref name="data"/>.</param>
        /// <returns><see langword="true"/> if the chunk was written; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> or <paramref name="length"/> is negative, or
        /// <paramref name="length"/> is greater than the length of <paramref name="data"/>.
        /// </exception>
        /// <remarks>
        /// Callers invoke this in a loop with sequential offsets. The first write (<paramref
        /// name="offset"/> == 0) erases the secondary slot to guarantee clean storage and validates
        /// the new image before accepting further chunks. Staging is decoupled from applying: writing
        /// chunks does not itself schedule a swap - the caller applies the update on its own terms
        /// (for the deployment image: reboot, then <see cref="ConfirmDeploymentImage"/> after
        /// validation, or <see cref="RequestDeploymentRevert"/>; for the nanoCLR image:
        /// <see cref="RequestClrRevert"/> or a host-driven flow).
        /// This method is stateless across calls; only one thread should stage a given image at a
        /// time, since interleaved offsets from two writers corrupt the staged image.
        /// </remarks>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool StoreImageChunk(ImageType image, byte[] data, int offset, int length);

        //----------------------------------------------------------------------
        // Confirm / revert
        //----------------------------------------------------------------------

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

        //----------------------------------------------------------------------
        // Reboot
        //----------------------------------------------------------------------

        /// <summary>
        /// Reboots the device. Any swap that has been scheduled (test/permanent/revert) is applied
        /// during the reboot.
        /// </summary>
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void RequestReboot();
    }
}
