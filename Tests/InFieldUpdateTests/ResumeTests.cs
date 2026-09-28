//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using InFieldUpdateTests.Helpers;
using nanoFramework.Runtime.InFieldUpdate;
using nanoFramework.TestFramework;

namespace InFieldUpdateTests
{
    [TestClass]
    public class ResumeTests
    {
        // spans several 32 kB erase blocks on the external flash, so a pause at 60 % lands past the
        // block holding the header (the resume rewind would otherwise discard everything)
        private const int PayloadLength = 64 * 1024;

        [Setup]
        public void Setup() => TestSlot.Reset();

        [Cleanup]
        public void Cleanup() => TestSlot.Reset();

        [TestMethod]
        public void Resume_AfterPause_ContinuesAndCompletes()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(PayloadLength);
            byte[] header = McuBootImageBuilder.GetHeaderBytes(image);
            int partial = (image.Length * 6) / 10;

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);
            Assert.IsTrue(TestSlot.WriteChunks(session, image, partial));

            // pause: close the session but keep what was stored
            Assert.IsTrue(UpdateManager.AbortUpdateSession(session, false));
            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));

            UpdateSession resumed = UpdateManager.ResumeUpdateSession(TestSlot.Image, image.Length, header);

            Assert.IsNotNull(resumed, "the partial image should be resumable");
            Assert.IsTrue(resumed.IsResumed);
            Assert.AreEqual(image.Length, resumed.TotalLength);
            Assert.IsTrue(resumed.NextOffset > 0, "the header block is kept");
            Assert.IsTrue(resumed.NextOffset <= partial, "resume never claims more than was stored");
            Assert.IsTrue(resumed.Token != session.Token, "a resumed session gets a new token");

            // the header was read back from flash
            Assert.IsNotNull(resumed.Version);
            Assert.AreEqual(1, resumed.Version.Major);
            Assert.AreEqual(2, resumed.Version.Minor);
            Assert.AreEqual(McuBootImageBuilder.DefaultHeaderSize, resumed.HeaderSize);
            Assert.AreEqual(PayloadLength, resumed.ImageSize);

            Assert.IsTrue(TestSlot.WriteChunks(resumed, image, image.Length));
            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.CompleteUpdateSession(resumed));
        }

        [TestMethod]
        public void Resume_WithErasedValueTail_ContinuesAndCompletes()
        {
            TestSlot.EnsureNoPendingSwap();

            // the stored tail looks like erased flash, so the high-water mark under-estimates;
            // the caller simply re-sends those bytes
            byte[] image = McuBootImageBuilder.Build(PayloadLength, 1, 2, 3, 4, 0x5A, 8192);
            byte[] header = McuBootImageBuilder.GetHeaderBytes(image);
            int partial = McuBootImageBuilder.DefaultHeaderSize + PayloadLength - 4096;

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);
            Assert.IsTrue(TestSlot.WriteChunks(session, image, partial));
            Assert.IsTrue(UpdateManager.AbortUpdateSession(session, false));

            UpdateSession resumed = UpdateManager.ResumeUpdateSession(TestSlot.Image, image.Length, header);

            Assert.IsNotNull(resumed);
            Assert.IsTrue(resumed.NextOffset <= partial);

            Assert.IsTrue(TestSlot.WriteChunks(resumed, image, image.Length));
            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.CompleteUpdateSession(resumed));
        }

        [TestMethod]
        public void Resume_AfterFullImageStored_IsReadyToComplete()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(4096);
            byte[] header = McuBootImageBuilder.GetHeaderBytes(image);

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);
            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));
            Assert.IsTrue(UpdateManager.AbortUpdateSession(session, false));

            UpdateSession resumed = UpdateManager.ResumeUpdateSession(TestSlot.Image, image.Length, header);

            Assert.IsNotNull(resumed);
            Assert.AreEqual(image.Length, resumed.NextOffset);
            Assert.IsTrue(resumed.IsComplete);
            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.CompleteUpdateSession(resumed));
        }

        [TestMethod]
        public void Resume_WithoutExpectedHeader_IsAccepted()
        {
            TestSlot.EnsureNoPendingSwap();

            // large enough that the stored part reaches past the erase block holding the header
            byte[] image = McuBootImageBuilder.Build(PayloadLength);
            int partial = (image.Length * 6) / 10;

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);
            Assert.IsTrue(TestSlot.WriteChunks(session, image, partial));
            Assert.IsTrue(UpdateManager.AbortUpdateSession(session, false));

            UpdateSession resumed = UpdateManager.ResumeUpdateSession(TestSlot.Image, image.Length, null);

            Assert.IsNotNull(resumed, "the header check is optional");
            UpdateManager.AbortUpdateSession(resumed, false);
        }

        [TestMethod]
        public void Resume_WithDifferentHeader_FailsWithHeaderMismatch()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] stored = McuBootImageBuilder.Build(8192, 1, 2, 3, 4);
            byte[] other = McuBootImageBuilder.Build(8192, 9, 9, 9, 9);

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, stored.Length);
            Assert.IsTrue(TestSlot.WriteChunks(session, stored, stored.Length / 2));
            Assert.IsTrue(UpdateManager.AbortUpdateSession(session, false));

            UpdateSession resumed = UpdateManager.ResumeUpdateSession(
                TestSlot.Image,
                stored.Length,
                McuBootImageBuilder.GetHeaderBytes(other));

            Assert.IsNull(resumed, "a different image is staged");
            Assert.AreEqual((int)UpdateSessionResult.HeaderMismatch, (int)UpdateManager.GetLastSessionError());
            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));
        }

        [TestMethod]
        public void Resume_WithWrongTotalLength_FailsWithHeaderMismatch()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(8192);
            byte[] header = McuBootImageBuilder.GetHeaderBytes(image);

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);
            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length / 2));
            Assert.IsTrue(UpdateManager.AbortUpdateSession(session, false));

            // the stored header describes a larger image than this total length allows
            UpdateSession resumed = UpdateManager.ResumeUpdateSession(TestSlot.Image, 2048, header);

            Assert.IsNull(resumed);
            Assert.AreEqual((int)UpdateSessionResult.HeaderMismatch, (int)UpdateManager.GetLastSessionError());
        }

        [TestMethod]
        public void Resume_OnErasedSlot_FailsWithNoImage()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(4096);

            Assert.IsTrue(UpdateManager.EraseSecondaryImage(TestSlot.Image));

            UpdateSession resumed = UpdateManager.ResumeUpdateSession(
                TestSlot.Image,
                image.Length,
                McuBootImageBuilder.GetHeaderBytes(image));

            Assert.IsNull(resumed, "there is nothing to resume");
            Assert.AreEqual((int)UpdateSessionResult.NoImage, (int)UpdateManager.GetLastSessionError());
        }

        [TestMethod]
        public void Resume_WhileSessionOpen_ReplacesOwnSession()
        {
            TestSlot.EnsureNoPendingSwap();

            // large enough that the stored part reaches past the erase block holding the header
            byte[] image = McuBootImageBuilder.Build(PayloadLength);
            byte[] header = McuBootImageBuilder.GetHeaderBytes(image);
            int partial = (image.Length * 6) / 10;

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);
            Assert.IsTrue(TestSlot.WriteChunks(session, image, partial));

            // resuming without pausing first: allowed for the same owner, and the old handle dies
            UpdateSession resumed = UpdateManager.ResumeUpdateSession(TestSlot.Image, image.Length, header);

            Assert.IsNotNull(resumed);
            Assert.IsFalse(UpdateManager.StoreImageChunk(session, image, 0, 64), "the previous session is stale");
            Assert.AreEqual((int)UpdateSessionResult.BadToken, (int)UpdateManager.GetLastSessionError());

            UpdateManager.AbortUpdateSession(resumed, false);
        }
    }
}
