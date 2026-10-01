//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Decides whether the running image is healthy, while it is on trial after an update
    /// (<see cref="UpdateStatus.Testing"/>).
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the image works as expected - the agent then confirms it with
    /// <see cref="UpdateManager.ConfirmDeploymentImage"/>; <see langword="false"/> to have it rolled
    /// back with <see cref="UpdateManager.RequestDeploymentRevert"/>.
    /// </returns>
    /// <remarks>
    /// Check whatever "healthy" means for the application, and include being able to reach the update
    /// provider: an image that confirms but cannot update is stuck in the field.
    /// </remarks>
    public delegate bool HealthCheck();
}
