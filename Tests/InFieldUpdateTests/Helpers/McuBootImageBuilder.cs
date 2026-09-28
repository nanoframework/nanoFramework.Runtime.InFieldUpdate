//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace InFieldUpdateTests.Helpers
{
    /// <summary>
    /// Builds MCUboot images in RAM for the tests: header, deterministic payload and a TLV area
    /// carrying the SHA-256 digest - everything <c>CompleteUpdateSession</c> verifies.
    /// </summary>
    /// <remarks>
    /// The images are unsigned. That is enough here because the device only checks structure and
    /// hash when a session completes; MCUboot would reject an unsigned image at boot and erase the
    /// slot, which is also the safety net should a test run die after a successful complete.
    /// </remarks>
    internal static class McuBootImageBuilder
    {
        /// <summary>MCUboot <c>IMAGE_MAGIC</c>.</summary>
        public const uint ImageMagic = 0x96f3b83d;

        /// <summary>MCUboot <c>IMAGE_TLV_INFO_MAGIC</c>.</summary>
        public const ushort TlvInfoMagic = 0x6907;

        /// <summary>MCUboot <c>IMAGE_TLV_SHA256</c>.</summary>
        public const ushort TlvSha256 = 0x10;

        /// <summary>Size of <c>struct image_header</c>.</summary>
        public const int ImageHeaderStructSize = 32;

        /// <summary>Header size used by the test images (ORGPAL_PALTHREE uses 0x400).</summary>
        public const int DefaultHeaderSize = 0x400;

        /// <summary>
        /// Builds a complete, hash-consistent image.
        /// </summary>
        /// <param name="payloadLength">Size of the payload between header and TLV area.</param>
        /// <param name="major">Header version major.</param>
        /// <param name="minor">Header version minor.</param>
        /// <param name="revision">Header version revision.</param>
        /// <param name="build">Header version build number.</param>
        /// <param name="payloadSeed">Seed of the deterministic payload pattern.</param>
        /// <param name="trailingErasedBytes">
        /// Number of payload bytes at the end of the payload to leave as 0xFF, to exercise the
        /// resume high-water mark when the stored tail looks like erased flash.
        /// </param>
        public static byte[] Build(
            int payloadLength,
            byte major = 1,
            byte minor = 2,
            ushort revision = 3,
            uint build = 4,
            byte payloadSeed = 0x5A,
            int trailingErasedBytes = 0)
        {
            int headerSize = DefaultHeaderSize;
            int tlvAreaSize = 4 + 4 + 32; // TLV info + one TLV header + digest
            byte[] image = new byte[headerSize + payloadLength + tlvAreaSize];

            // struct image_header
            WriteUInt32(image, 0, ImageMagic);
            WriteUInt32(image, 4, 0);                       // ih_load_addr
            WriteUInt16(image, 8, (ushort)headerSize);      // ih_hdr_size
            WriteUInt16(image, 10, 0);                      // ih_protect_tlv_size
            WriteUInt32(image, 12, (uint)payloadLength);    // ih_img_size
            WriteUInt32(image, 16, 0);                      // ih_flags
            image[20] = major;                              // ih_ver.iv_major
            image[21] = minor;                              // ih_ver.iv_minor
            WriteUInt16(image, 22, revision);               // ih_ver.iv_revision
            WriteUInt32(image, 24, build);                  // ih_ver.iv_build_num
            WriteUInt32(image, 28, 0);                      // _pad1

            // payload: cheap deterministic pattern, optionally ending in a run of 0xFF
            int patternLength = payloadLength - trailingErasedBytes;

            for (int i = 0; i < payloadLength; i++)
            {
                image[headerSize + i] = (i < patternLength) ? (byte)((i * 31 + payloadSeed) & 0xFF) : (byte)0xFF;
            }

            // TLV area: info header, then the SHA-256 of header + payload
            int tlvOffset = headerSize + payloadLength;

            WriteUInt16(image, tlvOffset, TlvInfoMagic);
            WriteUInt16(image, tlvOffset + 2, (ushort)tlvAreaSize);
            WriteUInt16(image, tlvOffset + 4, TlvSha256);
            WriteUInt16(image, tlvOffset + 6, 32);

            byte[] digest = Sha256.ComputeHash(image, 0, tlvOffset);

            for (int i = 0; i < digest.Length; i++)
            {
                image[tlvOffset + 8 + i] = digest[i];
            }

            return image;
        }

        /// <summary>
        /// Builds an image whose TLV total length does not match its contents, so that
        /// <c>CompleteUpdateSession</c> reports <c>BadTlv</c>.
        /// </summary>
        public static byte[] BuildWithBrokenTlv(int payloadLength)
        {
            byte[] image = Build(payloadLength);
            int tlvOffset = DefaultHeaderSize + payloadLength;

            // claim a TLV area far larger than what was actually stored
            WriteUInt16(image, tlvOffset + 2, 0x7FFF);

            return image;
        }

        /// <summary>
        /// Returns a copy of the first <see cref="ImageHeaderStructSize"/> bytes - what
        /// <c>ResumeUpdateSession</c> takes as the expected header.
        /// </summary>
        public static byte[] GetHeaderBytes(byte[] image)
        {
            byte[] header = new byte[ImageHeaderStructSize];

            for (int i = 0; i < header.Length; i++)
            {
                header[i] = image[i];
            }

            return header;
        }

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }
}
