//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace InFieldUpdateTests.Helpers
{
    /// <summary>
    /// Minimal SHA-256, needed to build the <c>IMAGE_TLV_SHA256</c> entry of the test images.
    /// nanoFramework has no managed SHA-256 implementation (only HMACSHA256 in
    /// System.Security.Cryptography, which is not available to this test project).
    /// </summary>
    internal static class Sha256
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
