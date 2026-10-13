//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Sets how long a new deployment has to confirm itself before the device reboots and reverts it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Apply it to the application entry point (<c>Main</c>). When the deployment is running as an unconfirmed
    /// test image, the CLR starts a countdown before <c>Main</c> runs. If <see cref="UpdateManager.ConfirmDeploymentImage"/>
    /// isn't called before it expires, the device is reset and the bootloader reverts to the previous deployment.
    /// </para>
    /// <para>
    /// The countdown is armed at most once per power cycle and only for an unconfirmed deployment. It is cancelled by a
    /// successful confirmation or when a debugger session starts. A value of 0 disables it.
    /// </para>
    /// <para>
    /// The value is read by the CLR from the attribute metadata. Platforms without support for it ignore the attribute.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class DeploymentConfirmationTimeoutAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentConfirmationTimeoutAttribute"/> class.
        /// </summary>
        /// <param name="seconds">Time, in seconds, the deployment has to confirm itself. 0 disables the countdown.</param>
        public DeploymentConfirmationTimeoutAttribute(uint seconds)
        {
        }
    }
}
