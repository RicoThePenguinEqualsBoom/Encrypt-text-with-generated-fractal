﻿using System.Text;

namespace SteganoTool
{
    internal class ImageSize
    {
        private const int MinWidth = 500;
        private const int MinHeight = 500;
        private const int BitsForLength = 32;
        private const double SafetyMargin = 1.1;
        private const double DefaultAspectRatio = 1.5;
        private const int AesSize = 16;

        internal static (int width, int height) CalculateMinimumSize(string text, int width, int height)
        {
            if (width < 0 || height < 0)
            {
                MessageBox.Show("Width and height must be non-negative.");
            }

            int requiredPixels = CalculateRequiredPixels(text);

            if (requiredPixels <= width * height)
            {
                return (width, height);
            }

            double aspectRatio = CalculateAspectRatio(width, height);

            var (sugWidth, sugHeight) = CalculateOptimalDimensions(requiredPixels, aspectRatio);

            return (sugWidth, sugHeight);
        }

        internal static (int width, int height) ValidSize(int width, int height, string text)
        {
            var (sugWidth, sugHeight) = CalculateMinimumSize(text, width, height);

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
            int textLength = Encoding.UTF8.GetByteCount(text);
            int paddedLength = ((textLength + AesSize - 1) / AesSize) * AesSize;
            int encryptedSize = paddedLength + AesSize;

            return BitsForLength + (encryptedSize * 8);
        }

        private static double CalculateAspectRatio(int width, int height)
        {
            return width > 0 && height > 0
                ? (double)height / width
                : DefaultAspectRatio;
        }

        private static (int width, int height) CalculateOptimalDimensions(int requiredPixels, double aspectRatio)
        {
            double baseWidth = Math.Sqrt(requiredPixels / aspectRatio);
            double baseHeight = baseWidth * aspectRatio;

            int sugWidth = (int)(Math.Max(Math.Ceiling(baseWidth), MinWidth) * SafetyMargin);
            int sugHeight = (int)(Math.Max(Math.Ceiling(baseHeight), MinHeight) * SafetyMargin);

            return (sugWidth, sugHeight);
        }

        internal static bool IsBigEnough(int width, int height, int messageLength)
        {
            int totalBits = width * height * 3;
            int neededBits = (messageLength + 4) * 8;
            return totalBits >= neededBits;
        }
    }
}
