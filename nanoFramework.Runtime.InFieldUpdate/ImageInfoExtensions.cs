//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace nanoFramework.Runtime.InFieldUpdate
{
    /// <summary>
    /// Extension methods for arrays of <see cref="ImageInfo"/>, such as the one returned by
    /// <see cref="UpdateManager.GetImageList"/>.
    /// </summary>
    public static class ImageInfoExtensions
    {
        internal static readonly string[] Headers = new string[]
        {
            "Image", "Slot", "Version", "Valid", "Active", "Confirmed", "Pending", "Bootable", "Rollback"
        };

        /// <summary>
        /// Formats a list of <see cref="ImageInfo"/> entries as an ASCII table, one row per entry.
        /// </summary>
        /// <param name="images">The entries to format, typically the result of <see cref="UpdateManager.GetImageList"/>.</param>
        /// <returns>A multi-line string containing the formatted table.</returns>
        public static string ToTable(this ImageInfo[] images)
        {
            if ((images == null) || (images.Length == 0))
            {
                return "(no images)";
            }

            string[][] rows = new string[images.Length][];

            for (int i = 0; i < images.Length; i++)
            {
                rows[i] = GetRow(images[i]);
            }

            int[] widths = GetColumnWidths(rows);

            string separator = BuildSeparator(widths);

            string table = separator + "\r\n" + BuildRow(Headers, widths) + "\r\n" + separator + "\r\n";

            for (int i = 0; i < rows.Length; i++)
            {
                table += BuildRow(rows[i], widths) + "\r\n";
            }

            table += separator;

            return table;
        }

        internal static string[] GetRow(ImageInfo image) => new string[]
            {
                GetImageName(image.Image),
                image.Slot.ToString(),
                image.Version == null ? "-" : image.Version.ToString(),
                YesNo(image.HasValidHeader),
                YesNo(image.IsActive),
                YesNo(image.IsConfirmed),
                YesNo(image.IsPending),
                YesNo(image.IsBootable),
                YesNo(image.IsRollbackPending)
            };

        private static string YesNo(bool value)
        {
            return value ? "Yes" : "No";
        }

        internal static string GetImageName(ImageType image)
        {
            return image switch
            {
                ImageType.NanoClr => "nanoCLR",
                ImageType.Deployment => "Deployment",
                _ => image.ToString(),
            };
        }

        private static int[] GetColumnWidths(string[][] rows)
        {
            int[] widths = new int[Headers.Length];

            for (int c = 0; c < Headers.Length; c++)
            {
                widths[c] = Headers[c].Length;
            }

            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < widths.Length; c++)
                {
                    int length = rows[r][c].Length;
                    if (length > widths[c])
                    {
                        widths[c] = length;
                    }
                }
            }

            return widths;
        }

        private static string BuildSeparator(int[] widths)
        {
            string line = "+";

            for (int c = 0; c < widths.Length; c++)
            {
                line += new string('-', widths[c] + 2) + "+";
            }

            return line;
        }

        private static string BuildRow(string[] values, int[] widths)
        {
            string line = "|";

            for (int c = 0; c < values.Length; c++)
            {
                line += " " + values[c].PadRight(widths[c], ' ') + " |";
            }

            return line;
        }
    }
}
