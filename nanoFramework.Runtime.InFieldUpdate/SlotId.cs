//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Identifies an image slot.
    /// </summary>
    public enum SlotId
    {
        /// <summary>
        /// Primary slot - the active execution slot.
        /// </summary>
        Primary = 0,

        /// <summary>
        /// Secondary slot - staging area for new images.
        /// </summary>
        Secondary = 1
    }
}
