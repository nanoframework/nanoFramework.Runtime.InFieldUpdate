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
    /// The whole update flow as an agent drives it - resume or start, download, complete - against
    /// the real <see cref="UpdateManager"/>, with <see cref="FakeUpdateProvider"/> standing in for
    /// the transport.
    /// </summary>
    [TestClass]
    public class ProviderFlowTests
    {
        // spans several 32 kB erase blocks on the external flash, so a pause at 60 % lands past the
        // block holding the header (the resume rewind would otherwise discard everything)
        private const int PayloadLength = 64 * 1024;

        // newer than any deployment image the test device runs
        private const byte OfferedMajor = 200;

        [Setup]
        public void Setup() => TestSlot.Reset();

        [Cleanup]
        public void Cleanup() => TestSlot.Reset();

        [TestMethod]
        public void Run_FullDownload_StagesImage()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(8192, OfferedMajor, 1, 2, 3);
            FakeUpdateProvider provider = new FakeUpdateProvider(image, new Version(OfferedMajor, 1, 2, 3));
            ReferenceAgent agent = new ReferenceAgent(provider, TestSlot.Image, new UpdateAgentOptions());

            Assert.AreEqual((int)AgentOutcome.Staged, (int)agent.Run());
            Assert.IsFalse(agent.Resumed, "the slot was empty, so the download started fresh");
            Assert.AreEqual((int)UpdateStatus.TestPending, (int)UpdateManager.GetStatus(TestSlot.Image));

            ImageInfo staged = UpdateManager.GetSecondaryImageInfo(TestSlot.Image);

            Assert.IsNotNull(staged);
            Assert.AreEqual((int)OfferedMajor, staged.Version.Major);
        }

        [TestMethod]
        public void Run_HonoursChunkSize()
        {
            TestSlot.EnsureNoPendingSwap();

            const int chunkSize = 1024;

            byte[] image = McuBootImageBuilder.Build(8192, OfferedMajor);
            FakeUpdateProvider provider = new FakeUpdateProvider(image, new Version(OfferedMajor, 2, 3, 4));
            UpdateAgentOptions options = new UpdateAgentOptions { ChunkSize = chunkSize };

            Assert.AreEqual((int)AgentOutcome.Staged, (int)new ReferenceAgent(provider, TestSlot.Image, options).Run());

            Assert.AreEqual(chunkSize, provider.LargestBuffer, "no read may ask for more than ChunkSize");

            // one header probe, then the whole image in ChunkSize pieces
            int expectedReads = 1 + (image.Length + chunkSize - 1) / chunkSize;
            Assert.AreEqual(expectedReads, provider.ReadCount);
        }

        [TestMethod]
        public void Run_TransportFailure_PausesAndNextRunResumes()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(PayloadLength, OfferedMajor);
            int failAt = (image.Length * 6) / 10;

            FakeUpdateProvider provider = new FakeUpdateProvider(image, new Version(OfferedMajor, 2, 3, 4))
            {
                FailAtOffset = failAt
            };

            ReferenceAgent agent = new ReferenceAgent(provider, TestSlot.Image, new UpdateAgentOptions());

            Assert.AreEqual((int)AgentOutcome.Paused, (int)agent.Run());
            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image), "a paused download holds no session");

            // the connection is back
            provider.ResetCounters();

            Assert.AreEqual((int)AgentOutcome.Staged, (int)agent.Run());
            Assert.IsTrue(agent.Resumed, "the second run continues the paused download");
            Assert.IsTrue(provider.FirstPayloadReadOffset > 0, "the stored part is not downloaded again");
            Assert.IsTrue(provider.FirstPayloadReadOffset <= failAt, "resume never skips bytes that were not stored");
        }

        [TestMethod]
        public void Run_CorruptPackage_RetriesUpToCapThenRejects()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(4096, OfferedMajor);

            // one flipped payload byte: every download ends in HashMismatch
            image[McuBootImageBuilder.DefaultHeaderSize + 100] ^= 0xFF;

            FakeUpdateProvider provider = new FakeUpdateProvider(image, new Version(OfferedMajor, 2, 3, 4));
            UpdateAgentOptions options = new UpdateAgentOptions { MaxRetries = 2 };
            ReferenceAgent agent = new ReferenceAgent(provider, TestSlot.Image, options);

            Assert.AreEqual((int)AgentOutcome.Rejected, (int)agent.Run());
            Assert.AreEqual(options.MaxRetries + 1, agent.FailedAttempts);
            Assert.AreEqual((int)UpdateSessionResult.HashMismatch, (int)agent.LastResult);

            // nothing scheduled, nothing left to resume
            Assert.AreEqual((int)UpdateStatus.Confirmed, (int)UpdateManager.GetStatus(TestSlot.Image));
            Assert.IsNull(UpdateManager.GetSecondaryImageInfo(TestSlot.Image), "the rejected image is erased");
        }

        [TestMethod]
        public void Run_DifferentImageInSlot_StartsFresh()
        {
            TestSlot.EnsureNoPendingSwap();

            // a paused download of some other image is sitting in the slot
            byte[] other = McuBootImageBuilder.Build(PayloadLength, 9, 9, 9, 9);
            UpdateSession session = TestSlot.Open(other.Length);
            Assert.IsTrue(TestSlot.WriteChunks(session, other, other.Length / 2));
            Assert.AreEqual((int)UpdateSessionResult.Success, (int)UpdateManager.AbortUpdateSession(session, false));

            byte[] image = McuBootImageBuilder.Build(PayloadLength, OfferedMajor);
            FakeUpdateProvider provider = new FakeUpdateProvider(image, new Version(OfferedMajor, 2, 3, 4));
            ReferenceAgent agent = new ReferenceAgent(provider, TestSlot.Image, new UpdateAgentOptions());

            Assert.AreEqual((int)AgentOutcome.Staged, (int)agent.Run());
            Assert.IsFalse(agent.Resumed, "the header did not match, so the slot was erased and restarted");
            Assert.AreEqual(0, provider.FirstPayloadReadOffset);
        }

        [TestMethod]
        public void Run_NothingOnOffer_LeavesSlotUntouched()
        {
            TestSlot.EnsureNoPendingSwap();

            byte[] image = McuBootImageBuilder.Build(4096, OfferedMajor);
            FakeUpdateProvider provider = new FakeUpdateProvider(image, new Version(OfferedMajor, 2, 3, 4))
            {
                NoOffer = true
            };

            Assert.AreEqual(
                (int)AgentOutcome.NothingOnOffer,
                (int)new ReferenceAgent(provider, TestSlot.Image, new UpdateAgentOptions()).Run());

            Assert.AreEqual(0, provider.ReadCount);
            Assert.IsNull(UpdateManager.GetSecondaryImageInfo(TestSlot.Image));
            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(TestSlot.Image));
        }

        [TestMethod]
        public void Run_OfferNotNewer_IsUpToDate()
        {
            TestSlot.EnsureNoPendingSwap();

            if (UpdateManager.GetPrimaryImageInfo(TestSlot.Image) == null)
            {
                Assert.SkipTest("no running deployment image to compare with");
            }

            byte[] image = McuBootImageBuilder.Build(4096, 0, 0, 0, 0);
            FakeUpdateProvider provider = new FakeUpdateProvider(image, new Version(0, 0, 0, 0));

            Assert.AreEqual(
                (int)AgentOutcome.UpToDate,
                (int)new ReferenceAgent(provider, TestSlot.Image, new UpdateAgentOptions()).Run());

            Assert.AreEqual(0, provider.ReadCount);
        }
    }
}
