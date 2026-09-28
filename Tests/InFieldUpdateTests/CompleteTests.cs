////
//// Licensed to the .NET Foundation under one or more agreements.
//// The .NET Foundation licenses this file to you under the MIT license.
////

//using InFieldUpdateTests.Helpers;
//using nanoFramework.Runtime.InFieldUpdate;
//using nanoFramework.TestFramework;

//namespace InFieldUpdateTests
//{
//    [TestClass]
//    public class CompleteTests
//    {
//        [Setup]
//        public void Setup() => TestSlot.Reset();

//        [Cleanup]
//        public void Cleanup() => TestSlot.Reset();

//        [TestMethod]
//        public void Complete_BeforeAllBytesStored_ReportsIncompleteAndKeepsSessionOpen()
//        {
//            TestSlot.EnsureNoPendingSwap();

//            byte[] image = McuBootImageBuilder.Build(4096);
//            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);

//            Assert.IsTrue(TestSlot.WriteChunks(session, image, 2048));

//            Assert.AreEqual((int)UpdateSessionResult.Incomplete, (int)UpdateManager.CompleteUpdateSession(session));
//            Assert.AreEqual((int)UpdateSessionResult.Incomplete, (int)UpdateManager.GetLastSessionError());
//            Assert.AreEqual((int)UpdateSessionOwner.Managed, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));

//            // the session survived, so the download can carry on
//            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));
//            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.CompleteUpdateSession(session));
//        }

//        [TestMethod]
//        public void Complete_WithCorruptPayload_ReportsHashMismatch()
//        {
//            TestSlot.EnsureNoPendingSwap();

//            byte[] image = McuBootImageBuilder.Build(4096);

//            // one flipped payload byte is enough: the digest in the TLV area no longer matches
//            image[McuBootImageBuilder.DefaultHeaderSize + 100] ^= 0xFF;

//            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);
//            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));

//            Assert.AreEqual((int)UpdateSessionResult.HashMismatch, (int)UpdateManager.CompleteUpdateSession(session));

//            // the session is closed and nothing was scheduled
//            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));
//            Assert.AreEqual((int)UpdateStatus.Confirmed, (int)UpdateManager.GetStatus(TestSlot.Image));
//        }

//        [TestMethod]
//        public void Complete_WithBrokenTlvArea_ReportsBadTlv()
//        {
//            TestSlot.EnsureNoPendingSwap();

//            byte[] image = McuBootImageBuilder.BuildWithBrokenTlv(4096);
//            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);

//            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));

//            Assert.AreEqual((int)UpdateSessionResult.BadTlv, (int)UpdateManager.CompleteUpdateSession(session));
//            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));
//        }

//        [TestMethod]
//        public void Complete_WithValidImage_MarksSlotPending()
//        {
//            TestSlot.EnsureNoPendingSwap();

//            byte[] image = McuBootImageBuilder.Build(8192, 3, 4, 5, 6);
//            byte[] expectedHash = Sha256.ComputeHash(image, 0, McuBootImageBuilder.DefaultHeaderSize + 8192);

//            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);
//            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));

//            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.CompleteUpdateSession(session));
//            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));

//            ImageInfo staged = UpdateManager.GetSecondaryImageInfo(TestSlot.Image);

//            Assert.IsNotNull(staged, "the staged image must be visible in the secondary slot");
//            Assert.AreEqual((int)SlotId.Secondary, (int)staged.Slot);
//            Assert.IsTrue(staged.HasValidHeader);
//            Assert.IsTrue(staged.IsPending, "a completed session schedules a test swap");
//            Assert.AreEqual(3, staged.Version.Major);
//            Assert.AreEqual(4, staged.Version.Minor);
//            Assert.AreEqual(5, staged.Version.Build);
//            Assert.AreEqual(6, staged.Version.Revision);

//            Assert.IsNotNull(staged.ImageHash);
//            Assert.AreEqual(32, staged.ImageHash.Length);

//            for (int i = 0; i < expectedHash.Length; i++)
//            {
//                Assert.AreEqual(expectedHash[i], staged.ImageHash[i], "hash byte " + i.ToString());
//            }

//            Assert.AreEqual((int)UpdateStatus.TestPending, (int)UpdateManager.GetStatus(TestSlot.Image));

//            // undo the scheduled swap so the run leaves nothing behind
//            Assert.IsTrue(UpdateManager.EraseSecondaryImage(TestSlot.Image));
//            Assert.IsNull(UpdateManager.GetSecondaryImageInfo(TestSlot.Image));
//            Assert.AreEqual((int)UpdateStatus.Confirmed, (int)UpdateManager.GetStatus(TestSlot.Image));
//        }

//        [TestMethod]
//        public void Complete_OnStaleSession_ReportsBadToken()
//        {
//            TestSlot.EnsureNoPendingSwap();

//            byte[] image = McuBootImageBuilder.Build(2048);
//            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);

//            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));
//            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.CompleteUpdateSession(session));

//            // the handle is stale now
//            Assert.AreEqual((int)UpdateSessionResult.BadToken, (int)UpdateManager.CompleteUpdateSession(session));

//            Assert.IsTrue(UpdateManager.EraseSecondaryImage(TestSlot.Image));
//        }
//    }
//}
