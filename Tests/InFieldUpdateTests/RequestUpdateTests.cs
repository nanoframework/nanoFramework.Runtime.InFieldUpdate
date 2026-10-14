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
    /// <summary>
    /// <see cref="UpdateManager.CompleteUpdateSession(UpdateSession, bool)"/> without requesting the
    /// update, followed by <see cref="UpdateManager.RequestUpdate(ImageType[])"/>. Like every other
    /// test, these only use the deployment slot.
    /// </summary>
    [TestClass]
    public class RequestUpdateTests
    {
        [Setup]
        public void Setup() => TestSlot.Reset();

        [Cleanup]
        public void Cleanup() => TestSlot.Reset();

        /// <summary>Stages and verifies an image without requesting the update.</summary>
        private static void StageWithoutRequest()
        {
            byte[] image = McuBootImageBuilder.Build(4096);
            UpdateSession session = TestSlot.Open(image.Length);

            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));
            Assert.AreEqual(
                (int)UpdateSessionResult.Success,
                (int)UpdateManager.CompleteUpdateSession(session, false),
                "the image should verify");
        }

        [TestMethod]
        public void CompleteWithoutRequest_LeavesImageVerifiedButNotPending()
        {
            TestSlot.EnsureNoPendingSwap();

            StageWithoutRequest();

            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image), "the session is closed");
            Assert.AreEqual((int)UpdateStatus.Confirmed, (int)UpdateManager.GetStatus(TestSlot.Image), "nothing should be pending yet");
            Assert.IsNotNull(UpdateManager.GetSecondaryImageInfo(TestSlot.Image), "the image stays in the slot");
        }

        [TestMethod]
        public void RequestUpdate_AfterCompleteWithoutRequest_MarksImagePending()
        {
            TestSlot.EnsureNoPendingSwap();

            StageWithoutRequest();

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.RequestUpdate(TestSlot.Image));
            Assert.AreEqual((int)UpdateStatus.TestPending, (int)UpdateManager.GetStatus(TestSlot.Image));

            TestSlot.EnsureNoPendingSwap();
        }

        [TestMethod]
        public void RequestUpdate_Twice_SecondReportsNoImage()
        {
            TestSlot.EnsureNoPendingSwap();

            StageWithoutRequest();

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.RequestUpdate(TestSlot.Image));
            Assert.AreEqual(
                (int)UpdateSessionResult.NoImage,
                (int)UpdateManager.RequestUpdate(TestSlot.Image),
                "an image is requested once");

            TestSlot.EnsureNoPendingSwap();
        }

        [TestMethod]
        public void RequestUpdate_WithoutCompleteWithoutRequest_ReportsNoImage()
        {
            TestSlot.EnsureNoPendingSwap();

            // a complete image in the slot is not enough: after a swap that would be the previous one
            byte[] image = McuBootImageBuilder.Build(4096);
            UpdateSession session = TestSlot.Open(image.Length);

            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));
            UpdateManager.AbortUpdateSession(session, false);

            Assert.AreEqual((int)UpdateSessionResult.NoImage, (int)UpdateManager.RequestUpdate(TestSlot.Image));
            Assert.AreEqual((int)UpdateStatus.Confirmed, (int)UpdateManager.GetStatus(TestSlot.Image));
        }

        [TestMethod]
        public void RequestUpdate_AfterCompleteWithRequest_ReportsNoImage()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(4096);
            UpdateSession session = TestSlot.Open(image.Length);

            Assert.IsTrue(TestSlot.WriteChunks(session, image, image.Length));
            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.CompleteUpdateSession(session, true));

            Assert.AreEqual((int)UpdateSessionResult.NoImage, (int)UpdateManager.RequestUpdate(TestSlot.Image));

            TestSlot.EnsureNoPendingSwap();
        }

        [TestMethod]
        public void RequestUpdate_AfterNewSession_ReportsNoImage()
        {
            TestSlot.EnsureNoPendingSwap();

            StageWithoutRequest();

            // opening a session on the image drops the verified image
            UpdateManager.AbortUpdateSession(TestSlot.Open(4096), false);

            Assert.AreEqual((int)UpdateSessionResult.NoImage, (int)UpdateManager.RequestUpdate(TestSlot.Image));
        }

        [TestMethod]
        public void RequestUpdate_AfterErase_ReportsNoImage()
        {
            TestSlot.EnsureNoPendingSwap();

            StageWithoutRequest();

            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.EraseSecondaryImage(TestSlot.Image));
            Assert.AreEqual((int)UpdateSessionResult.NoImage, (int)UpdateManager.RequestUpdate(TestSlot.Image));
        }

        [TestMethod]
        public void RequestUpdate_WhileSessionOpen_ReportsBusy()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = TestSlot.Open(4096);

            Assert.AreEqual((int)UpdateSessionResult.Busy, (int)UpdateManager.RequestUpdate(TestSlot.Image));

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void RequestUpdate_NoImages_ReportsBadArgument()
        {
            Assert.AreEqual((int)UpdateSessionResult.BadArgument, (int)UpdateManager.RequestUpdate());
        }

        [TestMethod]
        public void RequestUpdate_ImageListedTwice_ReportsBadArgument()
        {
            Assert.AreEqual(
                (int)UpdateSessionResult.BadArgument,
                (int)UpdateManager.RequestUpdate(TestSlot.Image, TestSlot.Image));
        }

        [TestMethod]
        public void RequestUpdate_UnknownImage_ReportsBadArgument()
        {
            Assert.AreEqual((int)UpdateSessionResult.BadArgument, (int)UpdateManager.RequestUpdate((ImageType)7));
        }

        [TestMethod]
        public void RequestUpdate_Null_Throws()
        {
            Assert.ThrowsException(typeof(ArgumentNullException), () => UpdateManager.RequestUpdate(null));
        }
    }
}
