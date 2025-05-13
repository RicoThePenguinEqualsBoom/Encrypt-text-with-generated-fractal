using System.Text;

namespace SteganoTool
{
    internal class ImageSize
    {
        //Define the constants for image size calculations
        private const int MinWidth = 500;
        private const int MinHeight = 500;
        private const int BitsForLength = 32;
        private const double SafetyMargin = 1.1;
        private const double DefaultAspectRatio = 1;
        private const int AesSize = 16;

        private static (int width, int height) CalculateMinimumSize(string text, int width, int height)
        {
            //Calculate the minimum required amount of pixels for the given text
            int requiredPixels = CalculateRequiredPixels(text);

            //If the given sizes are enough, don't change them
            if (requiredPixels <= width * height)
            {
                return (width, height);
            }

            //Make sure to retain the aspect ratio of the image
            double aspectRatio = CalculateAspectRatio(width, height);

            //Calculate the suggested dimensions based on the required pixels and aspect ratio
            var (sugWidth, sugHeight) = CalculateOptimalDimensions(requiredPixels, aspectRatio);

            return (sugWidth, sugHeight);
        }

        internal static (int width, int height) ValidSize(int width, int height, string text)
        {
            //If the image is too small, calculate the minimum required size
            var (sugWidth, sugHeight) = CalculateMinimumSize(text, width, height);

            //Compare the minimum size with the given size
            return (width < sugWidth, height < sugHeight) switch
            {
                (true, true) => (sugWidth, sugHeight),
                (true, false) => (sugWidth, height),
                (false, true) => (width, sugHeight),
                _ => (width, height)
            };
        }

        private static int CalculateRequiredPixels(string text)
        {
            //Calculate the amount of pixels required
            int textLength = Encoding.UTF8.GetByteCount(text);
            int paddedLength = ((textLength + AesSize - 1) / AesSize) * AesSize;
            int encryptedSize = paddedLength + AesSize;

            return BitsForLength + (encryptedSize * 8);
        }

        private static double CalculateAspectRatio(int width, int height)
        {
            //Get the wanted aspect ratio of the image
            return width > 0 && height > 0
                ? (double)height / width
                : DefaultAspectRatio;
        }

        private static (int width, int height) CalculateOptimalDimensions(int requiredPixels, double aspectRatio)
        {
            //Using the required pixels and aspect ratio calculate a base width and height
            double baseWidth = Math.Sqrt(requiredPixels / aspectRatio);
            double baseHeight = baseWidth * aspectRatio;

            //Using that base width and height, calculate the suggested dimensions
            int sugWidth = (int)(Math.Max(Math.Ceiling(baseWidth), MinWidth) * SafetyMargin);
            int sugHeight = (int)(Math.Max(Math.Ceiling(baseHeight), MinHeight) * SafetyMargin);

            return (sugWidth, sugHeight);
        }

        internal static bool IsBigEnough(int width, int height, int messageLength)
        {
            //Compare the amount of available bits to the amount of bits required
            int totalBits = width * height * 3;
            int neededBits = (messageLength + 4) * 8;
            return totalBits >= neededBits;
        }
    }
}
