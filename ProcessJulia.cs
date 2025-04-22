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
using System.Security.Cryptography;

namespace SteganoTool
{
    internal class ProcessJulia
    {
        private const double FractalScale = 1.5;
        private const int MaxIterations = 5000;

        internal static Bitmap GenerateJulia(double real, double imaginary, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height);

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0.ToPointer();
                int stride = bmpData.Stride;
                int minit = MaxIterations;
                int maxit = 0;
                int zero = 0;
                int total = width * height;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        double zx = FractalScale * (x - width / 2.0) / (0.5 * width);
                        double zy = (y - height / 2.0) / (0.5 * height);
                        int iteration = CalculateJuliaPoint(zx, zy, real, imaginary);
                        int px = y * stride + x * 4;

                        minit = Math.Min(minit, iteration);
                        maxit = Math.Max(maxit, iteration);
                        if (iteration == 0) zero++;

                        if (iteration == MaxIterations)
                        {
                            ptr[px] = 0;
                            ptr[px + 1] = 0;
                            ptr[px + 2] = 0;
                            ptr[px + 3] = 255;
                        }
                        else
                        {
                            double norm = (double)(iteration - 1) / (MaxIterations - 1);
                            double hue = 360.0 * norm;
                            Color colorValue = ColorFromHSV(hue, 1.0, 1.0);

                            ptr[px] = colorValue.B;
                            ptr[px + 1] = colorValue.G;
                            ptr[px + 2] = colorValue.R;
                            ptr[px + 3] = 255;
                        }
                    }
                }
                MessageBox.Show($" min : {minit} max : {maxit}, zeros : {zero} / {total}");
            }
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        internal static Bitmap EmbedDataLSB(Bitmap bmp, byte[] data)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            int capacity = width * height * 3 / 8;
            if (data.Length + 4 > capacity)
                throw new ArgumentException("message too big for image");

            Bitmap encrypted = bmp;

            byte[] lengthBytes = BitConverter.GetBytes(data.Length);
            byte[] fullData = lengthBytes.Concat(data).ToArray();

            int byteIdx = 0, bitIdx = 0;
            for (int y = 0; y < height && byteIdx < fullData.Length; y++)
            {
                for (int x = 0; x < width && byteIdx < fullData.Length; x++)
                {
                    Color pixel = encrypted.GetPixel(x, y);
                    byte[] rgb = { pixel.R, pixel.G, pixel.B };
                    for (int c = 0; c < 3 && byteIdx < fullData.Length; c++)
                    {
                        int bit = (fullData[byteIdx] >> (7 - bitIdx)) & 1;
                        rgb[c] = (byte)((rgb[c] & 0xFE) | bit);
                        bitIdx++;
                        if (bitIdx == 8)
                        {
                            bitIdx = 0;
                            byteIdx++;
                        }
                    }

                    Color newPixel = Color.FromArgb(rgb[0], rgb[1], rgb[2]);
                    encrypted.SetPixel(x, y, newPixel);
                }
            }

            return encrypted;
        }

        internal static byte[] ExtractDataLSB(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            byte[] buffer = new byte[width * height * 3 / 8];

            int byteIdx = 0, bitIdx = 0;
            int dataLen = -1;
            for (int y = 0; y < height && (dataLen == -1 || byteIdx < dataLen + 4); y++)
            {
                for (int x = 0; x < width && (dataLen == -1 || byteIdx < dataLen + 4); x++)
                {
                    Color pixel = bmp.GetPixel(x, y);
                    byte[] rgb = { pixel.R, pixel.G, pixel.B };
                    for (int c = 0; c < 3 && (dataLen == -1 || byteIdx < dataLen + 4); c++)
                    {
                        int bit = rgb[c] & 1;
                        buffer[byteIdx] = (byte)((buffer[byteIdx] << 1) | bit);
                        bitIdx++;
                        if (bitIdx == 8)
                        {
                            bitIdx = 0;
                            byteIdx++;
                            if (byteIdx == 4 && dataLen == -1)
                            {
                                dataLen = BitConverter.ToInt32(buffer, 0);
                            }
                        }
                    }
                }
            }

            if (dataLen < 0 || dataLen > buffer.Length - 4)
                throw new InvalidOperationException("no data");

            byte[] result = new byte[dataLen];
            Array.Copy(buffer, 4, result, 0, dataLen);
            return result;
        }

        private static int CalculateJuliaPoint(double zx, double zy, double real, double imaginary)
        {
            int iterations = 0;

            while (zx * zx + zy * zy < 4 && iterations < MaxIterations)
            {
                double temp = zx * zx - zy * zy + real;
                zy = 2.0 * zx * zy + imaginary;
                zx = temp;
                iterations++;
            }

            return iterations;
        }

        private static Color ColorFromHSV(double hue, double sat, double value)
        {
            hue = hue % 360;
            if (hue < 0) hue += 360;
            sat = Math.Clamp(sat, 0, 1);
            value = Math.Clamp(value, 0, 1);

            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = (int)value;
            int p = (int)(value * (1 - sat));
            int q = (int)(value * (1 - f * sat));
            int t = (int)(value * (1 - (1 - f) * sat));

            return hi switch
            {
                0 => Color.FromArgb(v, t, p),
                1 => Color.FromArgb(q, v, p),
                2 => Color.FromArgb(p, v, t),
                3 => Color.FromArgb(p, q, v),
                4 => Color.FromArgb(t, p, v),
                _ => Color.FromArgb(v, p, q)
            };
        }
    }
}
