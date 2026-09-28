//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace IFU_SessionUpdate
{
    /// <summary>
    /// Builds an MCUboot image in RAM for the sample: header, deterministic payload and a TLV area
    /// carrying the SHA-256 digest - everything an update session verifies when it completes.
    /// </summary>
    /// <remarks>
    /// The images are unsigned: enough for the session API, which checks structure and hash, but
    /// MCUboot would reject them at boot. A real update stages a package signed with imgtool.
    /// </remarks>
    public static class TestImage
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
        public static byte[] Build(
            int payloadLength,
            byte major = 1,
            byte minor = 2,
            ushort revision = 3,
            uint build = 4,
            byte payloadSeed = 0x5A)
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

            // payload: cheap deterministic pattern
            for (int i = 0; i < payloadLength; i++)
            {
                image[headerSize + i] = (byte)((i * 31 + payloadSeed) & 0xFF);
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
        /// Returns a copy of the first <see cref="ImageHeaderStructSize"/> bytes - what
        /// <c>UpdateManager.ResumeUpdateSession</c> takes as the expected header.
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

        /// <summary>
        /// Minimal SHA-256, needed to build the <c>IMAGE_TLV_SHA256</c> entry. nanoFramework has
        /// no managed SHA-256 implementation.
        /// </summary>
        private static class Sha256
        {
            private static readonly uint[] K = new uint[]
            {
                0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
                0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
                0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
                0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
                0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
                0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
                0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
                0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
            };

            /// <summary>
            /// Computes the SHA-256 digest of <paramref name="count"/> bytes of <paramref name="data"/>
            /// starting at <paramref name="offset"/>.
            /// </summary>
            public static byte[] ComputeHash(byte[] data, int offset, int count)
            {
                uint h0 = 0x6a09e667, h1 = 0xbb67ae85, h2 = 0x3c6ef372, h3 = 0xa54ff53a;
                uint h4 = 0x510e527f, h5 = 0x9b05688c, h6 = 0x1f83d9ab, h7 = 0x5be0cd19;

                // message + 0x80 + zero padding + 64-bit bit length, rounded up to a multiple of 64
                int paddedLength = ((count + 8) / 64 + 1) * 64;
                byte[] block = new byte[64];
                uint[] w = new uint[64];
                long bitLength = (long)count * 8;

                for (int blockStart = 0; blockStart < paddedLength; blockStart += 64)
                {
                    for (int i = 0; i < 64; i++)
                    {
                        int index = blockStart + i;

                        if (index < count)
                        {
                            block[i] = data[offset + index];
                        }
                        else if (index == count)
                        {
                            block[i] = 0x80;
                        }
                        else if (index >= paddedLength - 8)
                        {
                            int shift = (paddedLength - 1 - index) * 8;
                            block[i] = (byte)((bitLength >> shift) & 0xFF);
                        }
                        else
                        {
                            block[i] = 0;
                        }
                    }

                    for (int i = 0; i < 16; i++)
                    {
                        w[i] = ((uint)block[i * 4] << 24) | ((uint)block[i * 4 + 1] << 16) |
                               ((uint)block[i * 4 + 2] << 8) | block[i * 4 + 3];
                    }

                    for (int i = 16; i < 64; i++)
                    {
                        uint s0 = RotateRight(w[i - 15], 7) ^ RotateRight(w[i - 15], 18) ^ (w[i - 15] >> 3);
                        uint s1 = RotateRight(w[i - 2], 17) ^ RotateRight(w[i - 2], 19) ^ (w[i - 2] >> 10);
                        w[i] = w[i - 16] + s0 + w[i - 7] + s1;
                    }

                    uint a = h0, b = h1, c = h2, d = h3, e = h4, f = h5, g = h6, h = h7;

                    for (int i = 0; i < 64; i++)
                    {
                        uint s1 = RotateRight(e, 6) ^ RotateRight(e, 11) ^ RotateRight(e, 25);
                        uint ch = (e & f) ^ (~e & g);
                        uint temp1 = h + s1 + ch + K[i] + w[i];
                        uint s0 = RotateRight(a, 2) ^ RotateRight(a, 13) ^ RotateRight(a, 22);
                        uint maj = (a & b) ^ (a & c) ^ (b & c);
                        uint temp2 = s0 + maj;

                        h = g;
                        g = f;
                        f = e;
                        e = d + temp1;
                        d = c;
                        c = b;
                        b = a;
                        a = temp1 + temp2;
                    }

                    h0 += a; h1 += b; h2 += c; h3 += d;
                    h4 += e; h5 += f; h6 += g; h7 += h;
                }

                byte[] digest = new byte[32];

                WriteBigEndian(digest, 0, h0);
                WriteBigEndian(digest, 4, h1);
                WriteBigEndian(digest, 8, h2);
                WriteBigEndian(digest, 12, h3);
                WriteBigEndian(digest, 16, h4);
                WriteBigEndian(digest, 20, h5);
                WriteBigEndian(digest, 24, h6);
                WriteBigEndian(digest, 28, h7);

                return digest;
            }

            private static uint RotateRight(uint value, int bits) => (value >> bits) | (value << (32 - bits));

            private static void WriteBigEndian(byte[] buffer, int offset, uint value)
            {
                buffer[offset] = (byte)(value >> 24);
                buffer[offset + 1] = (byte)(value >> 16);
                buffer[offset + 2] = (byte)(value >> 8);
                buffer[offset + 3] = (byte)value;
            }
        }
    }
}
