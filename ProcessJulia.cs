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
        private const double EscapeRadius = 2.0;
        private const int MaxIterations = 1_000_000_000;        

        internal static Bitmap GenerateJulia(Complex c, int width, int height, string colorMethod)
        {
            Color[] palette = ColorChoise(colorMethod);
            double[,] fractal = GenerateSet(c, width, height);
            double maxVal = fractal.Cast<double>().Max();

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                int[] pixels = new int[width * height];

                Parallel.For(0, height, y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        double norm = Math.Pow(fractal[x, y] / maxVal, 0.7);
                        int colorIdx = (int)(norm * (palette.Length - 1));
                        colorIdx = Math.Min(palette.Length - 1, Math.Max(0, colorIdx));
                        Color color = palette[colorIdx];
                        pixels[y * width + x] = color.ToArgb();
                    }
                });

                Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

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

        private static double[,] GenerateSet(Complex c, int width, int height)
        {
            double[,] fractal = new double[width, height];
            double xMin = -1.5, xMax = 1.5;
            double yMin = -1.5, yMax = 1.5;

            Parallel.For(0, width, x =>
            {
                for (int y = 0; y < height; y++)
                {
                    double zx = xMin + (xMax - xMin) * x / (width - 1);
                    double zy = yMin + (yMax - yMin) * y / (height - 1);
                    Complex z = new Complex(zx, zy);
                    int iteration = 0;

                    while (iteration < MaxIterations && z.Magnitude < EscapeRadius)
                    {
                        z = z * z + c;
                        iteration++;
                    }

                    double smoothValue;
                    if (iteration < MaxIterations)
                    {
                        double logZn = Math.Log(z.Magnitude) / Math.Log(2);
                        double nu = Math.Log(logZn) / Math.Log(2);
                        smoothValue = iteration + 1 - nu;
                    }
                    else
                    {
                        smoothValue = MaxIterations;
                    }
                    fractal[x, y] = smoothValue;
                }
            });

            return fractal;
        }

        private static Color[] ColorChoise(string method)
        {
            Color[] colors;

            return method switch
            {
                "Classic" => colors = ClassicSet(),
                "Rainbow" => colors = RainbowFromHSV(),
                _ => colors = ClassicSet()
            };
        }

        private static Color[] ClassicSet(int steps = 256)
        {
            Color[] stops = new Color[]
            {
                Color.Black,
                Color.FromArgb(66, 30, 15),
                Color.FromArgb(25, 7, 26),
                Color.FromArgb(9, 1, 47),
                Color.FromArgb(4, 4, 73),
                Color.FromArgb(0, 7, 100),
                Color.FromArgb(12, 44, 138),
                Color.FromArgb(24, 82, 177),
                Color.FromArgb(57, 125, 209),
                Color.FromArgb(134, 181, 229),
                Color.FromArgb(211, 236, 248),
                Color.FromArgb(241, 233, 191),
                Color.FromArgb(248, 201, 95),
                Color.FromArgb(255, 170, 0),
                Color.FromArgb(204, 128, 0),
                Color.FromArgb(153, 87, 0),
                Color.FromArgb(106, 52, 3)
            };

            Color[] palette = new Color[steps];
            for (int i = 0; i < steps; i++)
            {
                double pos = (double)i / (steps - 1) * (stops.Length - 1);
                int idx = (int)pos;
                double frac = pos - idx;

                if (idx >= stops.Length - 1)
                {
                    palette[i] = stops[stops.Length - 1];
                }
                else
                {
                    Color c1 = stops[idx];
                    Color c2 = stops[idx + 1];
                    int r = (int)(c1.R + frac * (c2.R - c1.R));
                    int g = (int)(c1.G + frac * (c2.G - c1.G));
                    int b = (int)(c1.B + frac * (c2.B - c1.B));
                    palette[i] = Color.FromArgb(255, r, g, b);
                }
            }
            return palette;
        }

        private static Color[] RainbowFromHSV(int steps = 256, double sat = 1.0, double value = 1.0)
        {
            Color[] palette = new Color[steps];
            for (int i = 0; i < steps; i++)
            {
                double hue = 360.0 * i / steps;
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

                switch (hi)
                {
                    case 0:
                        palette[i] = Color.FromArgb(255, v, t, p);
                        break;
                    case 1:
                        palette[i] = Color.FromArgb(255, q, v, p);
                        break;
                    case 2:
                        palette[i] = Color.FromArgb(255, p, v, t);
                        break;
                    case 3:
                        palette[i] = Color.FromArgb(255, p, q, v);
                        break;
                    case 4:
                        palette[i] = Color.FromArgb(255, t, p, v);
                        break;
                    default:
                        palette[i] = Color.FromArgb(255, v, p, q);
                        break;
                };
            }

            return palette;
        }
    }
}
