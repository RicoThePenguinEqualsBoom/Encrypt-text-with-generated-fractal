using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteganoTool
{
    internal class ImageSize
    {
        private const int MinPixelsPerParameter = 16;

        private const int ParameterCount = 3;

        private const int MinPatternSize = 32;

        internal static (int width, int height) CalculateMinimumSize(string text)
        {
            int textBytes = Encoding.UTF8.GetByteCount(text);

            int minPixelsForData = textBytes * MinPixelsPerParameter * ParameterCount;

            int minTotalPixels = Math.Max(minPixelsForData, MinPatternSize * MinPatternSize);

            int minDimensions = (int)Math.Ceiling(Math.Sqrt(minTotalPixels));

            return (minDimensions, minDimensions);
        }

        internal static bool IsValidSize(int width, int height, string text)
        {
            var (minWidth, minHeight) = CalculateMinimumSize(text);
            return width >= minWidth && height >= minHeight;
        }
    }
}
