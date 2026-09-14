//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Identifies an updateable image.
    /// </summary>
    /// <remarks>
    /// The nanoCLR firmware and the deployment (managed application) are updated independently,
    /// each with its own primary and secondary slot.
    /// </remarks>
    public enum ImageType
    {
        /// <summary>
        /// The nanoCLR firmware image.
        /// </summary>
        NanoClr = 0,

        /// <summary>
        /// The deployment (managed application) image.
        /// </summary>
        Deployment = 1
    }
}
