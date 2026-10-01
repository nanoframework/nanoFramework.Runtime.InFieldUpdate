//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using nanoFramework.Runtime.InFieldUpdate;
using nanoFramework.TestFramework;

namespace InFieldUpdateTests
{
    [TestClass]
    public class UpdateAgentOptionsTests
    {
        [TestMethod]
        public void Defaults()
        {
            UpdateAgentOptions options = new UpdateAgentOptions();

            Assert.AreEqual(UpdateAgentOptions.DefaultChunkSize, options.ChunkSize);
            Assert.AreEqual(UpdateAgentOptions.DefaultMaxRetries, options.MaxRetries);
            Assert.IsNull(options.ConfirmHook, "confirmation is the application's job by default");
        }

        [TestMethod]
        public void ChunkSize_NotPositive_Throws()
        {
            UpdateAgentOptions options = new UpdateAgentOptions();

            Assert.ThrowsException(typeof(ArgumentOutOfRangeException), () => options.ChunkSize = 0);
            Assert.ThrowsException(typeof(ArgumentOutOfRangeException), () => options.ChunkSize = -1);
            Assert.AreEqual(UpdateAgentOptions.DefaultChunkSize, options.ChunkSize, "a rejected value is not kept");
        }

        [TestMethod]
        public void MaxRetries_Negative_Throws()
        {
            UpdateAgentOptions options = new UpdateAgentOptions { MaxRetries = 0 };

            Assert.AreEqual(0, options.MaxRetries, "0 means a single attempt");
            Assert.ThrowsException(typeof(ArgumentOutOfRangeException), () => options.MaxRetries = -1);
        }

        [TestMethod]
        public void ConfirmHook_IsKept()
        {
            UpdateAgentOptions options = new UpdateAgentOptions { ConfirmHook = () => true };

            Assert.IsNotNull(options.ConfirmHook);
            Assert.IsTrue(options.ConfirmHook());
        }
    }
}
