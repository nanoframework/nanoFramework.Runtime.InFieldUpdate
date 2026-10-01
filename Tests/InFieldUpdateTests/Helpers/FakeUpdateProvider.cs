//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using nanoFramework.Runtime.InFieldUpdate;

namespace InFieldUpdateTests.Helpers
{
    /// <summary>
    /// <see cref="IUpdateProvider"/> serving an image from RAM, with injectable transport faults.
    /// </summary>
    /// <remarks>
    /// This is how an update agent is meant to be tested: real <see cref="UpdateManager"/> on a
    /// device, fake transport. It also records what the agent asked for, so tests can check that a
    /// download resumes where it stopped.
    /// </remarks>
    internal class FakeUpdateProvider : IUpdateProvider
    {
        private readonly byte[] _image;
        private readonly Version _version;
        private int _failAtOffset = -1;

        /// <summary>
        /// Serves <paramref name="image"/>, advertised with <paramref name="version"/>.
        /// </summary>
        public FakeUpdateProvider(byte[] image, Version version)
        {
            _image = image;
            _version = version;
            FirstPayloadReadOffset = -1;
        }

        /// <summary>Gets or sets a value indicating whether the provider reports nothing on offer.</summary>
        public bool NoOffer { get; set; }

        /// <summary>
        /// Gets or sets the image offset at which the transport drops, once: reads stop short of it and
        /// the next read starting at it fails with -1. Negative (the default) for no failure.
        /// </summary>
        public int FailAtOffset
        {
            get => _failAtOffset;
            set => _failAtOffset = value;
        }

        /// <summary>Gets the number of <see cref="Read"/> calls.</summary>
        public int ReadCount { get; private set; }

        /// <summary>Gets the largest buffer an agent passed to <see cref="Read"/>.</summary>
        public int LargestBuffer { get; private set; }

        /// <summary>
        /// Gets the offset of the first read past the header since <see cref="ResetCounters"/> -
        /// where the agent started (or resumed) the download. -1 when no such read happened.
        /// </summary>
        public int FirstPayloadReadOffset { get; private set; }

        /// <summary>Clears what was recorded so far.</summary>
        public void ResetCounters()
        {
            ReadCount = 0;
            LargestBuffer = 0;
            FirstPayloadReadOffset = -1;
        }

        /// <inheritdoc/>
        public bool TryGetOffer(out Version version, out int totalLength)
        {
            if (NoOffer)
            {
                version = null;
                totalLength = 0;
                return false;
            }

            version = _version;
            totalLength = _image.Length;
            return true;
        }

        /// <inheritdoc/>
        public int Read(int offset, byte[] buffer)
        {
            ReadCount++;

            if (buffer.Length > LargestBuffer)
            {
                LargestBuffer = buffer.Length;
            }

            // the 32-byte header probe at offset 0 is not where the download starts
            if (FirstPayloadReadOffset < 0 && !(offset == 0 && buffer.Length == McuBootImageBuilder.ImageHeaderStructSize))
            {
                FirstPayloadReadOffset = offset;
            }

            if (_failAtOffset >= 0 && offset >= _failAtOffset)
            {
                // connection lost: fail once, then serve normally again
                _failAtOffset = -1;
                return -1;
            }

            int count = _image.Length - offset;

            if (count > buffer.Length)
            {
                count = buffer.Length;
            }

            if (_failAtOffset >= 0 && offset + count > _failAtOffset)
            {
                // stop short of the failure point, as a stream would before the connection drops
                count = _failAtOffset - offset;
            }

            if (count <= 0)
            {
                return 0;
            }

            Array.Copy(_image, offset, buffer, 0, count);

            return count;
        }
    }
}
