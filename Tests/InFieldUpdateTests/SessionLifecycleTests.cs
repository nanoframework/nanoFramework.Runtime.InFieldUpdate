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
    public class SessionLifecycleTests
    {
        [Setup]
        public void Setup() => TestSlot.Reset();

        [Cleanup]
        public void Cleanup() => TestSlot.Reset();

        [TestMethod]
        public void StartUpdateSession_OpensSessionAndClaimsImage()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, 4096);

            Assert.IsNotNull(session, "session should be open");
            Assert.AreEqual((int)TestSlot.Image, (int)session.Image);
            Assert.AreEqual(4096, session.TotalLength);
            Assert.AreEqual(0, session.NextOffset);
            Assert.IsFalse(session.IsResumed);
            Assert.IsFalse(session.IsComplete);
            Assert.IsTrue(session.Token != 0, "token should not be zero");
            Assert.AreEqual((int)UpdateSessionOwner.Managed, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));

            // the slot was erased when the session opened
            Assert.IsNull(UpdateManager.GetSecondaryImageInfo(TestSlot.Image), "slot should be erased");

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void StartUpdateSession_HeaderMembersUnknownBeforeFirstChunk()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, 4096);

            Assert.IsNull(session.Version, "version is only known once the header has been written");
            Assert.AreEqual(0, session.HeaderSize);
            Assert.AreEqual(0, session.ImageSize);

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void StartUpdateSession_WhileOpen_FailsWithBusy()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession first = UpdateManager.StartUpdateSession(TestSlot.Image, 4096);

            // same owner reopening replaces its own session, so simulate a second writer by
            // checking that the image reports itself claimed and the slot cannot be erased
            Assert.AreEqual((int)UpdateSessionOwner.Managed, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));
            Assert.IsFalse(UpdateManager.EraseSecondaryImage(TestSlot.Image), "erase must be refused while a session is open");
            Assert.AreEqual((int)UpdateSessionResult.Busy, (int)UpdateManager.GetLastSessionError());

            UpdateManager.AbortUpdateSession(first, false);
        }

        [TestMethod]
        public void StartUpdateSession_SameOwnerReopening_ReplacesPreviousSession()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession abandoned = UpdateManager.StartUpdateSession(TestSlot.Image, 4096);
            UpdateSession replacement = UpdateManager.StartUpdateSession(TestSlot.Image, 8192);

            Assert.IsNotNull(replacement, "the same owner may reopen its own session");
            Assert.IsTrue(abandoned.Token != replacement.Token, "a new session gets a new token");
            Assert.AreEqual(8192, replacement.TotalLength);

            byte[] image = McuBootImageBuilder.Build(64);
            Assert.IsFalse(UpdateManager.StoreImageChunk(abandoned, image, 0, image.Length), "the replaced session is stale");
            Assert.AreEqual((int)UpdateSessionResult.BadToken, (int)UpdateManager.GetLastSessionError());

            UpdateManager.AbortUpdateSession(replacement, false);
        }

        [TestMethod]
        public void StartUpdateSession_LargerThanSlot_FailsWithTooLarge()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, 64 * 1024 * 1024);

            Assert.IsNull(session, "an image larger than the slot cannot be staged");
            Assert.AreEqual((int)UpdateSessionResult.TooLarge, (int)UpdateManager.GetLastSessionError());
            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));
        }

        [TestMethod]
        public void StartUpdateSession_NonPositiveLength_Throws()
        {
            TestSlot.EnsureNoPendingSwap();

            Assert.ThrowsException(
                typeof(ArgumentOutOfRangeException),
                () => UpdateManager.StartUpdateSession(TestSlot.Image, 0));

            Assert.ThrowsException(
                typeof(ArgumentOutOfRangeException),
                () => UpdateManager.StartUpdateSession(TestSlot.Image, -1));
        }

        [TestMethod]
        public void AbortUpdateSession_ReleasesImage()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, 4096);

            Assert.IsTrue(UpdateManager.AbortUpdateSession(session, false));
            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));
            Assert.IsTrue(UpdateManager.EraseSecondaryImage(TestSlot.Image), "erase works once the session is closed");
        }

        [TestMethod]
        public void AbortUpdateSession_OnStaleSession_Fails()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, 4096);

            Assert.IsTrue(UpdateManager.AbortUpdateSession(session, false));
            Assert.IsFalse(UpdateManager.AbortUpdateSession(session, false), "the session is already closed");
            Assert.AreEqual((int)UpdateSessionResult.BadToken, (int)UpdateManager.GetLastSessionError());
        }

        [TestMethod]
        public void SessionOperations_WithNullSession_Throw()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] data = new byte[8];

            Assert.ThrowsException(
                typeof(ArgumentNullException),
                () => UpdateManager.StoreImageChunk(null, data, 0, data.Length));

            Assert.ThrowsException(
                typeof(ArgumentNullException),
                () => UpdateManager.CompleteUpdateSession(null));

            Assert.ThrowsException(
                typeof(ArgumentNullException),
                () => UpdateManager.AbortUpdateSession(null, false));
        }
    }
}
