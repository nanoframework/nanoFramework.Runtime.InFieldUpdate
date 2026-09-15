//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//


using nanoFramework.Runtime.InFieldUpdate;
using System;
using System.Diagnostics;
using System.Threading;

namespace IFU_Mangement
{
    public class Program
    {
        public static void Main()
        {
            Debug.WriteLine("Hello from nanoFramework IFU test app!");

            // list all images
            var images = UpdateManager.GetImageList();

            // print all images
            Console.WriteLine(images.ToTable());

            var primaryImageInfo = UpdateManager.GetPrimaryImageInfo(ImageType.Deployment);
            var secondaryImageInfo = UpdateManager.GetSecondaryImageInfo(ImageType.Deployment);

            if (primaryImageInfo != null)
            {
                Console.WriteLine($"{primaryImageInfo.ToString()}");
            }

            if (secondaryImageInfo != null)
            {
                Console.WriteLine($"{secondaryImageInfo.ToString()}");
            }

            //// erase deployment secondary slot
            //UpdateManager.EraseSecondaryImage(ImageType.Deployment);

            //// list all images again
            //images = UpdateManager.GetImageList();

            //// print all images again
            //Console.WriteLine(images.ToTable());

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
