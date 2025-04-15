using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteganoTool
{
    internal class ImageSize
    {
        private const int MinWidth = 400;

        private const int MinHeight = 300;

        private const int BitsForLength = 32;

        internal static (int width, int height) CalculateMinimumSize(string text, int width, int height)
        {
            if (width < MinWidth || height < MinHeight)
            {
                var textBytes = Encoding.UTF8.GetBytes(text);
                var encryptedSize = ((textBytes.Length / 16) + 1) * 16 + 16;
                var requiredPixels = BitsForLength + (encryptedSize * 8);

                double aspectRatio = width > 0 ? (double)height / width : 1.5;
                var sugWidth = (int)(Math.Max((int)Math.Ceiling(Math.Sqrt(requiredPixels * width / height)), MinWidth) * 1.1);
                var sugHeight = (int)(Math.Max((int)Math.Ceiling(requiredPixels / (double)sugWidth), MinHeight) * 1.1);

                return (sugWidth, sugHeight);
            }
            else
            {
                return (width, height);
            }
        }

        internal static (int, int) IsValidSize(int width, int height, string text)
        {
            var (sugWidth, sugHeight) = CalculateMinimumSize(text, width, height);

            if (width < sugWidth && height < sugHeight)
            {
                return (sugWidth, sugHeight);
            }
            else if (width < sugWidth && height >= sugHeight)
            {
                return (sugWidth, height);
            }
            else if (width >= sugWidth && height < sugHeight)
            {
                return (width, sugHeight);
            }
            else
            {
                return (width, height);
            }
        }
    }
}
