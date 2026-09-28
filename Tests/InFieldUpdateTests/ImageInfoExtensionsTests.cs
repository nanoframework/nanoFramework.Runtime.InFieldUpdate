//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using nanoFramework.Runtime.InFieldUpdate;
using nanoFramework.TestFramework;

namespace InFieldUpdateTests
{
    [TestClass]
    public class ImageInfoExtensionsTests
    {
        [TestMethod]
        public void ToTable_WithNoImages_ReportsEmpty()
        {
            Assert.AreEqual("(no images)", ((ImageInfo[])null).ToTable());
            Assert.AreEqual("(no images)", new ImageInfo[0].ToTable());
        }

        [TestMethod]
        public void ToTable_WithDeviceImages_HasHeaderAndOneRowPerSlot()
        {
            string table = UpdateManager.GetImageList().ToTable();

            Assert.IsNotNull(table);
            Assert.IsTrue(table.IndexOf("Image") >= 0, "the table carries its column headers");
            Assert.IsTrue(table.IndexOf("nanoCLR") >= 0);
            Assert.IsTrue(table.IndexOf("Deployment") >= 0);

            // header + separators + one row per image/slot
            int lines = 1;

            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] == '\n')
                {
                    lines++;
                }
            }

            Assert.IsTrue(lines >= 5, "at least a header and four rows");
        }
    }
}
