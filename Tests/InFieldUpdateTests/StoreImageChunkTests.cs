//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using InFieldUpdateTests.Helpers;
using nanoFramework.Runtime.InFieldUpdate;
using nanoFramework.TestFramework;

namespace InFieldUpdateTests
{
    [TestClass]
    public class StoreImageChunkTests
    {
        [Setup]
        public void Setup() => TestSlot.Reset();

        [Cleanup]
        public void Cleanup() => TestSlot.Reset();

        [TestMethod]
        public void StoreImageChunk_NullData_Throws()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = TestSlot.Open(4096);

            Assert.ThrowsException(
                typeof(ArgumentNullException),
                () => UpdateManager.StoreImageChunk(session, null, 0, 0));

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void StoreImageChunk_OffsetAndCountOutOfRange_Throw()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = TestSlot.Open(4096);
            byte[] data = new byte[64];

            Assert.ThrowsException(typeof(ArgumentOutOfRangeException), () => UpdateManager.StoreImageChunk(session, data, -1, 8));
            Assert.ThrowsException(typeof(ArgumentOutOfRangeException), () => UpdateManager.StoreImageChunk(session, data, 0, -1));
            Assert.ThrowsException(typeof(ArgumentOutOfRangeException), () => UpdateManager.StoreImageChunk(session, data, 60, 8));
            Assert.ThrowsException(typeof(ArgumentOutOfRangeException), () => UpdateManager.StoreImageChunk(session, data, 0, 65));

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void StoreImageChunk_FirstChunkWithoutMagic_FailsWithBadMagic()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = TestSlot.Open(4096);
            byte[] notAnImage = new byte[64];

            Assert.AreEqual(
                (int)UpdateSessionResult.BadMagic,
                (int)UpdateManager.StoreImageChunk(session, notAnImage, 0, notAnImage.Length));
            Assert.AreEqual(0, session.NextOffset, "a rejected chunk must not advance the position");

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void StoreImageChunk_FirstChunkShorterThanHeader_FailsWithBadMagic()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(256);
            UpdateSession session = TestSlot.Open(image.Length);

            // fewer bytes than struct image_header, so the header cannot be validated
            Assert.AreEqual(
                (int)UpdateSessionResult.BadMagic,
                (int)UpdateManager.StoreImageChunk(session, image, 0, 16));

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void StoreImageChunk_AdvancesPositionAndPublishesHeader()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(1024, 2, 5, 7, 11);
            UpdateSession session = TestSlot.Open(image.Length);

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.StoreImageChunk(session, image, 0, 512));
            Assert.AreEqual(512, session.NextOffset);

            // the header arrived with the first chunk, so its members are now readable
            Assert.IsNotNull(session.Version);
            Assert.AreEqual(2, session.Version.Major);
            Assert.AreEqual(5, session.Version.Minor);
            Assert.AreEqual(7, session.Version.Build);
            Assert.AreEqual(11, session.Version.Revision);
            Assert.AreEqual(McuBootImageBuilder.DefaultHeaderSize, session.HeaderSize);
            Assert.AreEqual(1024, session.ImageSize);

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.StoreImageChunk(session, image, 512, 512));
            Assert.AreEqual(1024, session.NextOffset);

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void StoreImageChunk_ZeroCount_IsNoOp()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(256);
            UpdateSession session = TestSlot.Open(image.Length);

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.StoreImageChunk(session, image, 0, 64));
            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.StoreImageChunk(session, image, 64, 0));
            Assert.AreEqual(64, session.NextOffset);

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void StoreImageChunk_PastTotalLength_FailsWithTooLarge()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(256);
            UpdateSession session = TestSlot.Open(image.Length);

            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));
            Assert.IsTrue(session.IsComplete);

            // one byte more than declared
            Assert.AreEqual(
                (int)UpdateSessionResult.TooLarge,
                (int)UpdateManager.StoreImageChunk(session, image, 0, 1));

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void StoreImageChunk_HonoursBufferOffset()
        {
            TestSlot.EnsureNoPendingSwap();

            // offset/count index into the buffer, Stream.Write style: staging the image out of a
            // larger buffer must produce exactly the same stored bytes, which Complete verifies
            // through the SHA-256 in the image's TLV area
            byte[] image = McuBootImageBuilder.Build(2048);
            byte[] oversized = new byte[image.Length + 777];

            for (int i = 0; i < image.Length; i++)
            {
                oversized[333 + i] = image[i];
            }

            UpdateSession session = TestSlot.Open(image.Length);

            int position = 0;

            while (position < image.Length)
            {
                int count = image.Length - position;

                if (count > 512)
                {
                    count = 512;
                }

                Assert.AreEqual(
                    (int)UpdateSessionResult.Success,
                    (int)UpdateManager.StoreImageChunk(session, oversized, 333 + position, count));
                position += count;
            }

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.CompleteUpdateSession(session));
        }

        [TestMethod]
        public void Write_AppendsWholeBuffer()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(512);
            UpdateSession session = TestSlot.Open(image.Length);

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)session.Write(image));
            Assert.AreEqual(image.Length, session.NextOffset);
            Assert.IsTrue(session.IsComplete);

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.CompleteUpdateSession(session));
        }

        [TestMethod]
        public void StoreImageChunk_OnStaleSession_FailsWithBadToken()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(256);
            UpdateSession session = TestSlot.Open(image.Length);

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.AbortUpdateSession(session, false));
            Assert.AreEqual(
                (int)UpdateSessionResult.BadToken,
                (int)UpdateManager.StoreImageChunk(session, image, 0, image.Length));
        }
    }
}
