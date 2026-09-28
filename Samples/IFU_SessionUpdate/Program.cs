//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Threading;
using nanoFramework.Runtime.InFieldUpdate;

namespace IFU_SessionUpdate
{
    /// <summary>
    /// Walks through a full update session: start, store chunks, pause, resume, complete.
    /// </summary>
    /// <remarks>
    /// The image is synthesised in RAM (see <see cref="TestImage"/>) so the sample runs without a
    /// server. A real application downloads the same bytes instead - the session calls are
    /// identical, only the source of each chunk differs. See the repository README for the HTTP
    /// range variant.
    /// </remarks>
    public class Program
    {
        // which image to stage into - never the nanoCLR one in a sample
        private const ImageType Image = ImageType.Deployment;

        private const int ChunkSize = 4096;

        public static void Main()
        {
            Console.WriteLine("IFU update session sample");
            Console.WriteLine(UpdateManager.GetImageList().ToTable());

            byte[] image = TestImage.Build(64 * 1024);

            if (!StageWithInterruption(image))
            {
                Console.WriteLine("staging failed, nothing scheduled");
                Thread.Sleep(Timeout.Infinite);
            }

            Console.WriteLine(UpdateManager.GetImageList().ToTable());

            // The image is now verified and marked for a test swap. A real application would
            // reboot here and, once the new image proved itself, call ConfirmDeploymentImage():
            //
            //     UpdateManager.RequestReboot();
            //
            // This sample stops short of that and discards the staged image instead, so it can be
            // run again without leaving a swap scheduled.
            Console.WriteLine("discarding the staged image (sample does not reboot)");
            UpdateManager.EraseSecondaryImage(Image);

            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
        /// Stages <paramref name="image"/>, deliberately pausing half way to show that the
        /// download picks up where it left off - which is what happens after a reboot, too.
        /// </summary>
        private static bool StageWithInterruption(byte[] image)
        {
            UpdateSession session = UpdateManager.StartUpdateSession(Image, image.Length);

            if (session == null)
            {
                Console.WriteLine($"could not start a session: {UpdateManager.GetLastSessionError()}");
                return false;
            }

            Console.WriteLine($"session open, {image.Length} bytes to store");

            if (!StoreChunks(session, image, image.Length / 2))
            {
                return false;
            }

            // Pause: close the session but keep what was stored. Passing false is what makes this
            // resumable; true would erase the slot.
            Console.WriteLine($"pausing at {session.NextOffset}");
            UpdateManager.AbortUpdateSession(session, false);

            // A real application reaches this point after a reboot, with nothing in memory. All it
            // needs is the image length and the first 32 bytes of the image it is downloading.
            byte[] expectedHeader = TestImage.GetHeaderBytes(image);

            session = UpdateManager.ResumeUpdateSession(Image, image.Length, expectedHeader);

            if (session == null)
            {
                // NoImage or HeaderMismatch mean "nothing useful is staged" - start over
                Console.WriteLine($"could not resume: {UpdateManager.GetLastSessionError()}");
                return false;
            }

            Console.WriteLine($"resumed at {session.NextOffset}, version {session.Version}");

            if (!StoreChunks(session, image, image.Length))
            {
                return false;
            }

            UpdateSessionResult result = UpdateManager.CompleteUpdateSession(session);

            Console.WriteLine($"complete: {result}");

            return result == UpdateSessionResult.Success;
        }

        /// <summary>
        /// Appends chunks until <paramref name="upTo"/> bytes of the image are stored.
        /// </summary>
        private static bool StoreChunks(UpdateSession session, byte[] image, int upTo)
        {
            while (session.NextOffset < upTo)
            {
                int count = upTo - session.NextOffset;

                if (count > ChunkSize)
                {
                    count = ChunkSize;
                }

                // offset/count index into the buffer; the slot position is session.NextOffset
                if (!UpdateManager.StoreImageChunk(session, image, session.NextOffset, count))
                {
                    Console.WriteLine($"chunk rejected at {session.NextOffset}: {UpdateManager.GetLastSessionError()}");
                    return false;
                }
            }

            return true;
        }
    }
}
