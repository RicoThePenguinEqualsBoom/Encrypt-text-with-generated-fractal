using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Threading.Tasks;
using System.Drawing;
using System.CodeDom.Compiler;
using System.Text;
using System.Collections;
using static System.Net.Mime.MediaTypeNames;

namespace SteganoTool
{
    internal class ProcessJulia : IDisposable
    {
        private const int MaxIterations = 300;
        private const double EscapeRadius = 2.0;
        private bool disposed;

        internal unsafe static Bitmap GenerateJulia(double realC, double imagC, int width, int height, string text)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(text);

            int requiredPixels = messageBytes.Length * 8 + 32;
            if (requiredPixels > width * height)
            {
                throw new ArgumentException("message too big");
            }

            var bmp = new Bitmap(width, height);

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                int* scan0 = (int*)bmpData.Scan0.ToPointer();
                int stride = bmpData.Stride >> 2;

                int messageLength = messageBytes.Length;
                for (int i = 0; i < 4; i++)
                {
                    int lengthByte = (messageLength >> (i * 8)) & 0xff;
                    scan0[i] = (0xFF << 24) | (lengthByte << 16) | (0 << 8) | 0;
                }

                Parallel.For(0, messageBytes.Length, byteIndex =>
                {
                    byte currentByte = messageBytes[byteIndex];
                    int basePixelIndex = 4 + (byteIndex * 8);

                    for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                    {
                        int pixelIndex = basePixelIndex + bitIndex;
                        int y = pixelIndex / width;
                        int x = pixelIndex % width;

                        bool bit = ((currentByte >> (7 - bitIndex)) & 1) == 1;
                        int* pixel = scan0 + (y* stride) + x;

                        double zx = (x - width / 2.0) / (width / 4.0);
                        double zy = (y - height / 2.0) / (height / 4.0);
                        int iteration = CalculateJuliaPoint(zx, zy, realC, imagC);
                        *pixel = CalculateColorWithMessage(iteration, bit);
                    }
                });

                Parallel.For(requiredPixels, width * height, pixelIndex =>
                {
                    int y = pixelIndex / width;
                    int x = pixelIndex % width;
                    int* pixel = scan0 + (y * stride) + x;

                    double zx = (x - width / 2.0) / (width / 4.0);
                    double zy = (y - height / 2.0) / (height / 4.0);
                    int iteration = CalculateJuliaPoint(zx, zy, realC, imagC);
                    *pixel = CalculateColor(iteration);
                });
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }

        internal static string DecodeJulia(Bitmap bmp, string key)
        {
            // Add validation for image format
            if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
            {
                throw new ArgumentException("Invalid image format. Image must be 32-bit ARGB.", nameof(bmp));
            }

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            var width = bmp.Width;
            var height = bmp.Height;

            try
            {
                unsafe
                {
                    int* scan0 = (int*)bmpData.Scan0.ToPointer();

                    int messageLength = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        int pixel = scan0[i];
                        int lengthByte = (pixel >> 16) & 0xFF;
                        messageLength |= (lengthByte << (i * 8));
                    }

                    MessageBox.Show($"message length extracted: {messageLength}");

                    if (messageLength <= 0 || messageLength > (width * height - 4) / 8)
                    {
                        throw new InvalidDataException("not good length");
                    }

                    byte[] messageBytes = new byte[messageLength];
                    int stride = bmpData.Stride >> 2;

                    for (int byteIndex = 0; byteIndex < messageLength; byteIndex++)
                    {
                        byte currentByte = 0;
                        int basePixelIndex = 4 + (byteIndex * 8);

                        for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                        {
                            int pixelIndex = basePixelIndex + bitIndex;
                            int y = pixelIndex / width;
                            int x = pixelIndex % width;
                            int pixel = scan0[y * stride + x];

                            bool bit = (pixel & 0x010000) != 0;
                            if (bit)
                            {
                                currentByte |= (byte)(1 << (7 -bitIndex));
                            }
                        }
                        messageBytes[byteIndex] = currentByte;
                    }

                    MessageBox.Show($"bytes extracted: {BitConverter.ToString(messageBytes)}");

                    var result = Encoding.UTF8.GetString(messageBytes);
                    MessageBox.Show($"result: {result}");
                    return result;
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        private static int CalculateJuliaPoint(double zx, double zy, double realC, double imagC)
        {
            int iterations = 0;
            double zx2 = zx * zx;
            double zy2 = zy * zy;

            while (zx2 + zy2 < EscapeRadius * EscapeRadius && iterations < MaxIterations)
            {
                zy = 2 * zx * zy + imagC;
                zx = zx2 - zy2 + realC;
                zx2 = zx * zx;
                zy2 = zy * zy;
                iterations++;
            }
            
            return iterations;
        }

        private static int CalculateColorWithMessage(int iteration, bool messageBit)
        {
            if (iteration == MaxIterations)
                return Color.Black.ToArgb();

            double smooth = (iteration + 1 - Math.Log(Math.Log(EscapeRadius))) / MaxIterations;
            smooth = Math.Clamp(smooth, 0, 1);

            double scaledPos = smooth * ()

            int r = (iteration * 9) % 256;
            int g = (iteration * 7) % 256;
            int b = (iteration * 5) % 256;

            r = (r & 0xFE) | (messageBit ? 1 : 0);

            return (0xFF << 24) | ((r << 16) | (g << 8) | b);
        }

        private static int CalculateColor(int iteration)
        {
            if (iteration == MaxIterations)
                return Color.Black.ToArgb();

            int r = (iteration * 9) % 256;
            int g = (iteration * 7) % 256;
            int b = (iteration * 5) % 256;

            return Color.Black.ToArgb() | ((r << 16) | (g << 8) | b);
        }

        private readonly Color[] gradientColors = new Color[]
        {
            Color.FromArgb(255, 0, 7, 100),
            Color.FromArgb(255, 32, 107, 203),
            Color.FromArgb(255, 237, 255, 255),
            Color.FromArgb(255, 255, 170, 0),
            Color.FromArgb(255, 180, 0, 0)
        };

        public void Dispose()
        {
            if (!disposed)
                disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
