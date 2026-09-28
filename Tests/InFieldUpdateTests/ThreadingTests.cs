//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System.Threading;
using InFieldUpdateTests.Helpers;
using nanoFramework.Runtime.InFieldUpdate;
using nanoFramework.TestFramework;

namespace InFieldUpdateTests
{
    [TestClass]
    public class ThreadingTests
    {
        [Setup]
        public void Setup() => TestSlot.Reset();

        [Cleanup]
        public void Cleanup() => TestSlot.Reset();

        [TestMethod]
        public void Session_IsOwnedByTheToken_NotByTheThread()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(4096);
            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);
            bool wroteFromOtherThread = false;

            // the token gates writes, so a session may legitimately be handed to another thread -
            // what it must not do is let two writers interleave, which the registry prevents
            Thread worker = new Thread(() =>
            {
                wroteFromOtherThread = UpdateManager.StoreImageChunk(session, image, 0, 1024);
            });

            worker.Start();
            worker.Join();

            Assert.IsTrue(wroteFromOtherThread, "a session works from whichever thread holds it");
            Assert.AreEqual(1024, session.NextOffset);

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void EraseFromOtherThread_WhileSessionOpen_IsRefused()
        {
            TestSlot.EnsureNoPendingSwap();

            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, 4096);
            bool erased = true;
            UpdateSessionResult error = UpdateSessionResult.Success;

            Thread worker = new Thread(() =>
            {
                erased = UpdateManager.EraseSecondaryImage(TestSlot.Image);
                error = UpdateManager.GetLastSessionError();
            });

            worker.Start();
            worker.Join();

            Assert.IsFalse(erased, "the slot is claimed by an open session");
            Assert.AreEqual((int)UpdateSessionResult.Busy, (int)error);

            UpdateManager.AbortUpdateSession(session, false);
        }

        [TestMethod]
        public void AbortFromOtherThread_InvalidatesTheSession()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(4096);
            UpdateSession session = UpdateManager.StartUpdateSession(TestSlot.Image, image.Length);

            Assert.IsTrue(UpdateManager.StoreImageChunk(session, image, 0, 1024));

            Thread worker = new Thread(() => UpdateManager.AbortUpdateSession(session, false));

            worker.Start();
            worker.Join();

            Assert.IsFalse(UpdateManager.StoreImageChunk(session, image, 1024, 1024), "the session was closed");
            Assert.AreEqual((int)UpdateSessionResult.BadToken, (int)UpdateManager.GetLastSessionError());
        }
    }
}
