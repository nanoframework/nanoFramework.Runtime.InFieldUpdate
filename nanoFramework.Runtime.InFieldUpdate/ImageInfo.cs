//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Metadata about a firmware image in a slot.
    /// </summary>
    /// <remarks>
    /// Each instance is self-identifying via <see cref="Image"/> and <see cref="Slot"/> - required
    /// because <see cref="UpdateManager.GetImageList"/> returns a flat array spanning every image
    /// and slot on the device.
    /// </remarks>
    public class ImageInfo
    {
        /// <summary>
        /// Gets which image this entry describes (nanoCLR or deployment).
        /// </summary>
        public ImageType Image { get; }

        /// <summary>
        /// Gets which slot this entry describes (primary or secondary).
        /// </summary>
        public SlotId Slot { get; }

        /// <summary>
        /// Gets the version of the image.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Gets the SHA-256 hash of the image (32 bytes), when available. <c>null</c> if it could
        /// not be read.
        /// </summary>
        public byte[] ImageHash { get; }

        /// <summary>
        /// Gets a value indicating whether the image metadata was read successfully. This is a
        /// structural check only - it does not verify the image hash or signature.
        /// </summary>
        public bool HasValidHeader { get; }

        /// <summary>
        /// Gets a value indicating whether this slot holds the currently running image.
        /// </summary>
        public bool IsActive { get; }

        /// <summary>
        /// Gets a value indicating whether this image is confirmed (permanent).
        /// </summary>
        public bool IsConfirmed { get; }

        /// <summary>
        /// Gets a value indicating whether a swap from this slot is pending on the next boot.
        /// </summary>
        public bool IsPending { get; }

        /// <summary>
        /// Gets a value indicating whether the image is allowed to run.
        /// </summary>
        public bool IsBootable { get; }

        /// <summary>
        /// Gets a value indicating whether a rollback affecting this image is scheduled for the
        /// next reboot.
        /// </summary>
        public bool IsRollbackPending { get; }

        // Instances are created only by native interop.
        internal ImageInfo()
        {
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Image + " " + Slot + ": v" + (Version == null ? "-" : Version.ToString());
        }
    }
}
