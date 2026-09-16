//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Represents the update state of a single image.
    /// </summary>
    public enum UpdateStatus
    {
        /// <summary>
        /// Running image is confirmed and no swap is scheduled. Normal operating state.
        /// </summary>
        Confirmed = 0,

        /// <summary>
        /// Running an unconfirmed test image. Unless confirmed, the previous image is restored on the next reboot.
        /// </summary>
        Testing = 1,

        /// <summary>
        /// A one-time test swap is scheduled for the next reboot.
        /// </summary>
        TestPending = 2,

        /// <summary>
        /// A permanent swap is scheduled for the next reboot, with no test cycle.
        /// </summary>
        PermanentPending = 3,

        /// <summary>
        /// A revert to the previous image is scheduled for the next reboot.
        /// </summary>
        RollbackPending = 4,

        /// <summary>
        /// State could not be determined.
        /// </summary>
        Unknown = 255
    }
}
