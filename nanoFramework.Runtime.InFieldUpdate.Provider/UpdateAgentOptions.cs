//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Settings an update agent built on <see cref="IUpdateProvider"/> takes from the application.
    /// </summary>
    /// <remarks>
    /// The defaults suit a typical deployment image downloaded over a network connection.
    /// </remarks>
    public class UpdateAgentOptions
    {
        /// <summary>
        /// Default value of <see cref="ChunkSize"/>, in bytes.
        /// </summary>
        public const int DefaultChunkSize = 4096;

        /// <summary>
        /// Default value of <see cref="MaxRetries"/>.
        /// </summary>
        public const int DefaultMaxRetries = 3;

        private int _chunkSize = DefaultChunkSize;
        private int _maxRetries = DefaultMaxRetries;

        /// <summary>
        /// Gets or sets the size, in bytes, of the buffer the image is streamed through: each
        /// <see cref="IUpdateProvider.Read(int, byte[])"/> asks for at most this many bytes, and each
        /// is stored with one <see cref="UpdateManager.StoreImageChunk(UpdateSession, byte[], int, int)"/>.
        /// </summary>
        /// <remarks>
        /// The buffer is allocated once per download, so this is also the RAM the download needs.
        /// The image is never held in RAM as a whole.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
        public int ChunkSize
        {
            get => _chunkSize;

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException();
                }

                _chunkSize = value;
            }
        }

        /// <summary>
        /// Gets or sets how many times a download is started over after the staged image fails
        /// verification (<see cref="UpdateSessionResult.HashMismatch"/>,
        /// <see cref="UpdateSessionResult.BadTlv"/> or <see cref="UpdateSessionResult.BadMagic"/>)
        /// before the agent gives up on the offered image.
        /// </summary>
        /// <remarks>
        /// A provider serving a broken package must not keep the device downloading forever. 0 means
        /// a single attempt.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        public int MaxRetries
        {
            get => _maxRetries;

            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException();
                }

                _maxRetries = value;
            }
        }

        /// <summary>
        /// Gets or sets the check the agent runs when the deployment image is on trial
        /// (<see cref="UpdateStatus.Testing"/>), to confirm or revert it before looking for a new
        /// update.
        /// </summary>
        /// <remarks>
        /// <c>null</c> (the default) leaves confirmation to the application: the agent then does not
        /// download anything while the running image is unconfirmed.
        /// </remarks>
        public HealthCheck ConfirmHook { get; set; }
    }
}
