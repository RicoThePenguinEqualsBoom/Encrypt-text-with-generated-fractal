using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Threading.Tasks;
using System.Drawing;
using System.CodeDom.Compiler;
using System.Text;
using System.Collections;

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
            var messageBits = new BitArray(messageBytes);

            var bmp = new Bitmap(width, height);

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                int* scan0 = (int*)bmpData.Scan0.ToPointer();
                int stride = bmpData.Stride >> 2;

                int processorCount = Environment.ProcessorCount;
                int blockSize = height / processorCount;

                Parallel.For(0, processorCount, threadIndex =>
                {
                    int startY = threadIndex * blockSize;
                    int endY = (threadIndex == processorCount - 1) ? height : (threadIndex + 1) * blockSize;

                    for (int y = startY; y < endY; y++)
                    {
                        int* row = scan0 + (y * stride);
                        double zy = (y - height / 2.0) / (height / 4.0);

                        for (int x = 0; x < width; x++)
                        {
                            double zx = (x - width / 2.0) / (width / 4.0);

                            int iteration = CalculateJuliaPoint(zx, zy, realC, imagC);

                            int color = CalculateColor(iteration, messageBits, x, y, width);
                            row[x] = color;
                        }
                    }
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
            var messageBuilder = new StringBuilder();
            var bitBuilder = new BitArray(8);
            int bitIndex = 0;

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    int* scan0 = (int*)bmpData.Scan0.ToPointer();
                    int stride = bmpData.Stride >> 2;

                    for (int y = 0; y < bmp.Height; y++)
                    {
                        int* row = scan0 + (y * stride);
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            int pixel = row[x];
                            bitBuilder[bitIndex++] = (pixel & 0x010000) != 0;

                            if (bitIndex == 8)
                            {
                                byte[] byteArray = new byte[1];
                                bitBuilder.CopyTo(byteArray, 0);
                                char c = (char)byteArray[0];

                                if (c == '\0')
                                    return messageBuilder.ToString();

                                messageBuilder.Append(c);
                                bitIndex = 0;
                            }
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return messageBuilder.ToString();
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

        private static int CalculateColor(int iteration, BitArray messageBits, int x, int y, int width)
        {
            if (iteration == MaxIterations)
                return Color.Black.ToArgb();

            int messageIndex = (y * width + x) % messageBits.Length;

            int r = (iteration * 9) % 256;
            int g = (iteration * 7) % 256;
            int b = (iteration * 5) % 256;

            if (messageIndex < messageBits.Length)
            {
                r = (r & 0xFE) | (messageBits[messageIndex] ? 1 : 0);
            }

            return Color.Black.ToArgb() | ((r << 16) | (g << 8) | b);
        }

        public void Dispose()
        {
            if (!disposed)
                disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
