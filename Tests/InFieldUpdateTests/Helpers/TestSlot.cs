//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using nanoFramework.Runtime.InFieldUpdate;
using nanoFramework.TestFramework;

namespace InFieldUpdateTests.Helpers
{
    /// <summary>
    /// Shared setup for the session tests.
    /// </summary>
    /// <remarks>
    /// Every test works on the <see cref="ImageType.Deployment"/> secondary slot only. The nanoCLR
    /// secondary slot is never touched: it may hold the image the device would roll back to.
    /// </remarks>
    internal static class TestSlot
    {
        /// <summary>The image every test stages into.</summary>
        public const ImageType Image = ImageType.Deployment;

        /// <summary>
        /// Leaves the deployment secondary slot erased and no session open, whatever state a
        /// previous test left behind.
        /// </summary>
        public static void Reset()
        {
            if (UpdateManager.GetUpdateSessionOwner(Image) == UpdateSessionOwner.Managed)
            {
                // reopening as the same owner replaces the abandoned session; aborting it with an
                // erase then leaves the slot clean
                if (UpdateManager.StartUpdateSession(Image, 1024, out UpdateSession session) == UpdateSessionResult.Success)
                {
                    UpdateManager.AbortUpdateSession(session, true);
                    return;
                }
            }

            UpdateManager.EraseSecondaryImage(Image);
        }

        /// <summary>
        /// Opens a fresh session for <paramref name="totalLength"/> bytes, failing the test if it
        /// cannot be opened.
        /// </summary>
        public static UpdateSession Open(int totalLength)
        {
            Assert.AreEqual(
                (int)UpdateSessionResult.Success,
                (int)UpdateManager.StartUpdateSession(Image, totalLength, out UpdateSession session),
                "session should open");

            Assert.IsNotNull(session);

            return session;
        }

        /// <summary>
        /// Clears a swap left pending by a previous test.
        /// </summary>
        /// <remarks>
        /// A successful <see cref="UpdateManager.CompleteUpdateSession"/> marks the staged image for a
        /// test swap, and while that is pending every <see cref="UpdateManager.StartUpdateSession"/>
        /// is refused with <see cref="UpdateSessionResult.SwapInFlight"/>. [Setup]/[Cleanup] run
        /// once per class, so each test calls this first - that also covers a previous test that
        /// failed before it could clean up. It only reads the swap state unless one is pending, so
        /// it costs no erase in the common case.
        /// </remarks>
        public static void EnsureNoPendingSwap()
        {
            UpdateStatus status = UpdateManager.GetStatus(Image);

            if (status == UpdateStatus.TestPending || status == UpdateStatus.PermanentPending)
            {
                // no session can be open while a swap is pending (Start/Resume refuse, Complete
                // closes), so the erase cannot be refused as Busy
                UpdateManager.EraseSecondaryImage(Image);
            }
        }

        /// <summary>
        /// Writes <paramref name="length"/> bytes of <paramref name="image"/> through
        /// <paramref name="session"/> in chunks.
        /// </summary>
        /// <returns><see langword="true"/> when every chunk was accepted.</returns>
        public static bool WriteChunks(UpdateSession session, byte[] image, int length, int chunkSize = 2048)
        {
            int position = session.NextOffset;

            while (position < length)
            {
                int count = length - position;

                if (count > chunkSize)
                {
                    count = chunkSize;
                }

                if (UpdateManager.StoreImageChunk(session, image, position, count) != UpdateSessionResult.Success)
                {
                    return false;
                }

                position += count;
            }

            return true;
        }
    }
}
