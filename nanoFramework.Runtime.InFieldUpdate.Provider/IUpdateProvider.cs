//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Source of update images for an update agent: an HTTP server, a cloud device-management
    /// service, MQTT, a file on an SD card... The provider knows where updates come from and how to
    /// fetch them; the agent decides when to update and drives <see cref="UpdateManager"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The image served must be the <b>signed MCUboot image, byte for byte</b>: header, payload and
    /// TLV area, as produced by the build. Any transformation breaks the SHA-256 check in
    /// <see cref="UpdateManager.CompleteUpdateSession(UpdateSession)"/> or the signature check at boot.
    /// </para>
    /// <para>
    /// <see cref="Read(int, byte[])"/> must accept any offset. An agent reads the first 32 bytes (the
    /// MCUboot header) to recognise a paused download of the same image, then continues from
    /// <see cref="UpdateSession.NextOffset"/>. A source that cannot seek can still comply by reading
    /// and discarding the bytes before the requested offset.
    /// </para>
    /// <para>
    /// An implementation that serves an image from memory, with injectable faults, is all it takes
    /// to test an agent against a real device.
    /// </para>
    /// </remarks>
    public interface IUpdateProvider
    {
        /// <summary>
        /// Asks the provider which image it currently offers.
        /// </summary>
        /// <param name="version">
        /// When this method returns <see langword="true"/>, the version in the MCUboot header of the
        /// offered image - the value <see cref="ImageInfo.Version"/> reports once it is running.
        /// </param>
        /// <param name="totalLength">
        /// When this method returns <see langword="true"/>, the exact size of the signed image in bytes.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when an image is on offer; <see langword="false"/> when there is
        /// nothing to offer or the provider could not be reached.
        /// </returns>
        bool TryGetOffer(out Version version, out int totalLength);

        /// <summary>
        /// Reads bytes of the offered image starting at <paramref name="offset"/>.
        /// </summary>
        /// <param name="offset">Zero-based position in the image of the first byte to read.</param>
        /// <param name="buffer">Buffer to fill, from its start.</param>
        /// <returns>
        /// The number of bytes read, at most <c>buffer.Length</c>; 0 at the end of the image; -1 on a
        /// transport error.
        /// </returns>
        /// <remarks>
        /// Returning fewer bytes than requested is allowed; the agent stores what it got and asks again
        /// from the new position.
        /// </remarks>
        int Read(int offset, byte[] buffer);
    }
}
