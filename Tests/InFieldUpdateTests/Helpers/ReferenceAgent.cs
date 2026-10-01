//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using nanoFramework.Runtime.InFieldUpdate;

namespace InFieldUpdateTests.Helpers
{
    /// <summary>
    /// What a <see cref="ReferenceAgent.Run"/> achieved.
    /// </summary>
    internal enum AgentOutcome
    {
        /// <summary>An image is staged and a reboot will apply it.</summary>
        Staged,

        /// <summary>The provider has nothing on offer.</summary>
        NothingOnOffer,

        /// <summary>The running image is already the offered version or newer.</summary>
        UpToDate,

        /// <summary>The device is not ready: unconfirmed image, swap pending, or slot busy.</summary>
        NotReady,

        /// <summary>The transport failed; the partial image was kept and the next run resumes it.</summary>
        Paused,

        /// <summary>The offered image failed verification more than the retry cap allows.</summary>
        Rejected,
    }

    /// <summary>
    /// Update agent written the way <c>docs/writing-an-update-provider.md</c> describes, on
    /// <see cref="IUpdateProvider"/> and <see cref="UpdateAgentOptions"/>. Test code, not part of the
    /// library: it is the code under test for the provider flow and the example the doc mirrors.
    /// </summary>
    internal class ReferenceAgent
    {
        private const int HeaderSize = 32;

        private readonly IUpdateProvider _provider;
        private readonly ImageType _image;
        private readonly UpdateAgentOptions _options;

        public ReferenceAgent(IUpdateProvider provider, ImageType image, UpdateAgentOptions options)
        {
            _provider = provider ?? throw new ArgumentNullException();
            _image = image;
            _options = options ?? new UpdateAgentOptions();
        }

        /// <summary>Gets the result of the last session operation that did not succeed.</summary>
        public UpdateSessionResult LastResult { get; private set; }

        /// <summary>Gets the number of downloads that ended in a verification failure in the last run.</summary>
        public int FailedAttempts { get; private set; }

        /// <summary>Gets a value indicating whether the last run continued a paused download.</summary>
        public bool Resumed { get; private set; }

        public AgentOutcome Run()
        {
            LastResult = UpdateSessionResult.Success;
            FailedAttempts = 0;
            Resumed = false;

            if (!SettleRunningImage())
            {
                return AgentOutcome.NotReady;
            }

            if (!_provider.TryGetOffer(out Version offered, out int totalLength))
            {
                return AgentOutcome.NothingOnOffer;
            }

            ImageInfo running = UpdateManager.GetPrimaryImageInfo(_image);

            if (running != null && !IsNewer(offered, running.Version))
            {
                return AgentOutcome.UpToDate;
            }

            byte[] header = new byte[HeaderSize];

            if (_provider.Read(0, header) != HeaderSize)
            {
                return AgentOutcome.Paused;
            }

            byte[] buffer = new byte[_options.ChunkSize];

            for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
            {
                // resume only on the first attempt: after a verification failure the slot holds
                // the bad image, which must be erased rather than continued
                UpdateSession session;
                UpdateSessionResult result = attempt == 0
                    ? OpenSession(totalLength, header, out session)
                    : UpdateManager.StartUpdateSession(_image, totalLength, out session);

                if (result != UpdateSessionResult.Success)
                {
                    // Busy, SwapInFlight, TooLarge, NoSlot, FlashError: a fresh start will not fix these
                    LastResult = result;
                    return AgentOutcome.NotReady;
                }

                if (!Download(session, buffer))
                {
                    // pause: the next Run() - even after a reboot - continues from the slot
                    UpdateManager.AbortUpdateSession(session, false);
                    return AgentOutcome.Paused;
                }

                result = UpdateManager.CompleteUpdateSession(session);

                if (result == UpdateSessionResult.Success)
                {
                    return AgentOutcome.Staged;
                }

                // HashMismatch / BadTlv / BadMagic: not the advertised image; start over
                LastResult = result;
                FailedAttempts++;
            }

            // give up, and make sure the next run does not resume the bad image
            UpdateManager.EraseSecondaryImage(_image);

            return AgentOutcome.Rejected;
        }

        private bool SettleRunningImage()
        {
            UpdateStatus status = UpdateManager.GetStatus(_image);

            if (status == UpdateStatus.Confirmed)
            {
                return true;
            }

            if (status != UpdateStatus.Testing || _options.ConfirmHook == null)
            {
                // a swap is pending, or confirming is the application's job
                return false;
            }

            if (_options.ConfirmHook())
            {
                return UpdateManager.ConfirmDeploymentImage();
            }

            UpdateManager.RequestDeploymentRevert();

            return false;
        }

        private UpdateSessionResult OpenSession(int totalLength, byte[] header, out UpdateSession session)
        {
            UpdateSessionResult result = UpdateManager.ResumeUpdateSession(_image, totalLength, header, out session);

            if (result == UpdateSessionResult.Success)
            {
                Resumed = true;
                return result;
            }

            if (result != UpdateSessionResult.NoImage && result != UpdateSessionResult.HeaderMismatch)
            {
                return result;
            }

            // nothing useful in the slot: start over (erases the whole slot)
            return UpdateManager.StartUpdateSession(_image, totalLength, out session);
        }

        private bool Download(UpdateSession session, byte[] buffer)
        {
            while (!session.IsComplete)
            {
                int read = _provider.Read(session.NextOffset, buffer);

                if (read <= 0)
                {
                    return false;
                }

                UpdateSessionResult result = UpdateManager.StoreImageChunk(session, buffer, 0, read);

                if (result != UpdateSessionResult.Success)
                {
                    // the position did not advance; this agent pauses rather than retrying
                    LastResult = result;
                    return false;
                }
            }

            return true;
        }

        private static bool IsNewer(Version a, Version b)
        {
            if (a.Major != b.Major) return a.Major > b.Major;
            if (a.Minor != b.Minor) return a.Minor > b.Minor;
            if (a.Build != b.Build) return a.Build > b.Build;
            return a.Revision > b.Revision;
        }
    }
}
