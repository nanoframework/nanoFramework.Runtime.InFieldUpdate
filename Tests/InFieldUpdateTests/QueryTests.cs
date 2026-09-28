//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using InFieldUpdateTests.Helpers;
using nanoFramework.Runtime.InFieldUpdate;
using nanoFramework.TestFramework;

namespace InFieldUpdateTests
{
    [TestClass]
    public class QueryTests
    {
        [Setup]
        public void Setup() => TestSlot.Reset();

        [Cleanup]
        public void Cleanup() => TestSlot.Reset();

        [TestMethod]
        public void GetImageList_ReturnsEverySlotOfEveryImage()
        {
            ImageInfo[] images = UpdateManager.GetImageList();

            Assert.IsNotNull(images);
            Assert.AreEqual(4, images.Length, "two images, two slots each");

            // entries are self-identifying, in image then slot order
            Assert.AreEqual((int)ImageType.NanoClr, (int)images[0].Image);
            Assert.AreEqual((int)SlotId.Primary, (int)images[0].Slot);
            Assert.AreEqual((int)ImageType.NanoClr, (int)images[1].Image);
            Assert.AreEqual((int)SlotId.Secondary, (int)images[1].Slot);
            Assert.AreEqual((int)ImageType.Deployment, (int)images[2].Image);
            Assert.AreEqual((int)SlotId.Primary, (int)images[2].Slot);
            Assert.AreEqual((int)ImageType.Deployment, (int)images[3].Image);
            Assert.AreEqual((int)SlotId.Secondary, (int)images[3].Slot);
        }

        [TestMethod]
        public void GetPrimaryImageInfo_ForRunningClr_DescribesItself()
        {
            ImageInfo clr = UpdateManager.GetPrimaryImageInfo(ImageType.NanoClr);

            Assert.IsNotNull(clr, "the running nanoCLR image must be readable");
            Assert.AreEqual((int)ImageType.NanoClr, (int)clr.Image);
            Assert.AreEqual((int)SlotId.Primary, (int)clr.Slot);
            Assert.IsTrue(clr.IsActive);
            Assert.IsTrue(clr.IsBootable);
            Assert.IsNotNull(clr.Version);

            if (clr.ImageHash != null)
            {
                Assert.AreEqual(32, clr.ImageHash.Length);
            }
        }

        [TestMethod]
        public void GetStatus_ForRunningClr_IsConfirmed()
        {
            // the CLR confirms itself during startup once its health checks pass
            Assert.AreEqual((int)UpdateStatus.Confirmed, (int)UpdateManager.GetStatus(ImageType.NanoClr));
        }

        [TestMethod]
        public void GetPrimaryImageInfo_ForRunningClr_IsConfirmedMatchesStatus()
        {
            // ImageInfo.IsConfirmed and GetStatus read the same trailer and must never disagree,
            // including for a CLR programmed directly (no trailer magic), which MCUboot never reverts
            ImageInfo clr = UpdateManager.GetPrimaryImageInfo(ImageType.NanoClr);

            Assert.IsNotNull(clr);
            Assert.AreEqual(
                UpdateManager.GetStatus(ImageType.NanoClr) == UpdateStatus.Confirmed,
                clr.IsConfirmed,
                "IsConfirmed must agree with GetStatus");
            Assert.IsTrue(clr.IsConfirmed, "the running CLR confirms itself during startup");
        }

        [TestMethod]
        public void GetSecondaryImageInfo_OnErasedSlot_IsNull()
        {
            Assert.IsTrue(UpdateManager.EraseSecondaryImage(TestSlot.Image));
            Assert.IsNull(UpdateManager.GetSecondaryImageInfo(TestSlot.Image));
        }

        [TestMethod]
        public void GetUpdateSessionOwner_WithNoSession_IsNone()
        {
            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(ImageType.NanoClr));
            Assert.AreEqual((int)UpdateSessionOwner.None, (int)UpdateManager.GetUpdateSessionOwner(ImageType.Deployment));
        }
    }
}
