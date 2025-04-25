using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Numerics;

namespace SteganoTool
{
    internal class ProcessFractal
    {
        private const double Epsilon = 1e-6;
        private const int MaxIterations = 200_000;
        private const double xMin = -1.5, yMin = -1.5;
        private const double xMax = 1.5, yMax = 1.5;

        private static readonly Complex[] roots =
        [
            new(1, 0),
            new(-0.5, Math.Sqrt(3)/2),
            new(-0.5, Math.Sqrt(3)/2)
        ];

        private delegate Complex FractalFunc(Complex z, Complex c);

        private static Complex NewtonFunc(Complex z, Complex c)
        {
            var fz = Complex.Pow(z, 3) - 1;
            var dfz = 3 * Complex.Pow(z, 2);
            if (dfz == Complex.Zero) return z;
            return z - fz / dfz;
        }

        private static Complex NovaFunc(Complex z, Complex c)
        {
            double alpha = 1.0;
            var fz = Complex.Pow(z, 3) - 1;
            var dfz = 3 * Complex.Pow(z, 2);
            if (dfz == Complex.Zero) return z + c;
            return z - alpha * (fz / dfz) + c;
        }

        private static Complex JuliaFunc(Complex z, Complex c)
        {
            return z * z + c;
        }

        private static bool HasConverged(Complex z)
        {
            foreach (var root in roots)
                if ((z - root).Magnitude < Epsilon)
                    return true;
            return false;
        }

        internal static Bitmap GenerateFractal(Complex c, int width, int height, double escapeRadius, string colorMethod,
            string fractalType)
        {
            FractalFunc func;
            Color[] palette;
            double[,] fractal;

            switch (fractalType)
            {
                case "Julia":
                    func = JuliaFunc;
                    palette = ColorChoice(colorMethod);
                    fractal = GenerateDivergingSet(c, width, height, escapeRadius, func);
                    break;
                case "Newton":
                    func = NewtonFunc;
                    palette = ColorChoice(colorMethod);
                    fractal = GenerateConvergingSet(c, width, height, func);
                    break;
                case "Nova":
                    func = NovaFunc;
                    palette = ColorChoice(colorMethod);
                    fractal = GenerateConvergingSet(c, width, height, func);
                    break;
                default:
                    throw new ArgumentException("no selected Fractal type");
            }
            double maxVal = fractal.Cast<double>().Max();

            Bitmap bmp = new (width, height, PixelFormat.Format32bppArgb);
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

        private static double[,] GenerateDivergingSet(Complex c, int width, int height, double escapeRadius, FractalFunc func)
        {
            double[,] fractal = new double[width, height];

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    double zx = xMin + (xMax - xMin) * x / (width - 1);
                    double zy = yMin + (yMax - yMin) * y / (height - 1);
                    Complex z = new (zx, zy);
                    int iteration = 0;

                    while (iteration < MaxIterations && z.Magnitude < escapeRadius)
                    {
                        z = func(z, c);
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

        private static double[,] GenerateConvergingSet(Complex c, int width, int height, FractalFunc func)
        {
            double[,] fractal = new double[width, height];

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    double zx = xMin + (xMax - xMin) * x / (width - 1);
                    double zy = yMin + (yMax - yMin) * y / (height - 1);
                    Complex z = new(zx, zy);
                    int iteration = 0;

                    while (iteration < MaxIterations && !HasConverged(z))
                    {
                        z = func(z, c);
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

        internal static Bitmap EmbedLSB(Bitmap bmp, byte[] data)
        {
            int width = bmp.Width, height = bmp.Height;
            int capacity = width * height * 3 / 8;

            if (data.Length + 4 > capacity)
                throw new ArgumentException("message too big for image");


            byte[] lengthBytes = BitConverter.GetBytes(data.Length);
            byte[] fullData = [.. lengthBytes, .. data];

            Bitmap encrypted = new (bmp);

            BitmapData bmpData = encrypted.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite,
                PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            int bytes = stride * height;
            byte[] pixelData = new byte[bytes];

            Marshal.Copy(bmpData.Scan0, pixelData, 0, bytes);

            int totalBits = fullData.Length * 8;
            for (int i = 0; i < pixelData.Length && i < totalBits; i++)
            {
                int byteIdx = i / 8;
                int bitIdx = 7 - (i % 8);
                int bit = (fullData[byteIdx] >> bitIdx) & 1;
                pixelData[i] = (byte)((pixelData[i] & 0xFE) | bit);
            }

            Marshal.Copy(pixelData, 0, bmpData.Scan0, bytes);
            encrypted.UnlockBits(bmpData);
            return encrypted;
        }

        internal static byte[] ExtractLSB(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            int maxBytes = width * height * 3 / 8;

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            int stride = bmpData.Stride;
            int bytes = stride * height;
            byte[] pixelData = new byte[bytes];

            Marshal.Copy(bmpData.Scan0, pixelData, 0, bytes);

            byte[] buffer = new byte[maxBytes];
            int dataLen = -1;

            for (int i = 0; i < pixelData.Length && (dataLen == -1 || i < (dataLen + 4) * 8); i++)
            {
                int byteIdx = i / 8;
                int bitIdx = 7 - (i % 8);
                int bit = pixelData[i] & 1;
                buffer[byteIdx] = (byte)((buffer[byteIdx] & ~(1 << bitIdx)) | (bit << bitIdx));

                if (i == 32 && dataLen == -1)
                {
                    dataLen = BitConverter.ToInt32(buffer, 0);
                }
            }

            bmp.UnlockBits(bmpData);

            if (dataLen < 0 || dataLen > maxBytes - 4)
                throw new InvalidOperationException("Invalid or no embedded data found");

            byte[] result = new byte[dataLen];
            Array.Copy(buffer, 4, result, 0, dataLen);
            return result;
        }

        internal static bool CheckIterations(Complex c, int width, int height, double escapeRadius, int samples = 250)
        {
            Random rng = new ();
            double totalIterations = 0;

            for (int i = 0; i < samples; i++)
            {
                int x = rng.Next(width);
                int y = rng.Next(height);

                double zx = xMin + (xMax - xMin) * x / (width - 1);
                double zy = yMin + (yMax - yMin) * y / (height - 1);
                Complex z = new (zx, zy);

                int iteration = 0;
                while (iteration < MaxIterations && z.Magnitude < escapeRadius)
                {
                    z = z * z + c;
                    iteration++;
                }
                totalIterations += iteration;
            }
            return totalIterations <= (double)(MaxIterations * 0.99999999999999999999999999999m);
        }

        private static Color[] ColorChoice(string method)
        {
            return method switch
            {
                "Classic" => ClassicSet(),
                "Rainbow" => RainbowFromHSV(),
                "Aurora" => Aurora(),
                _ => ClassicSet()
            };
        }

        private static Color[] ClassicSet()
        {
            Color[] stops =
            [
                Color.Black,
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
                Color.FromArgb(153, 87, 0),
                Color.Black
            ];

            return InterpolateColor(stops);
        }

        private static Color[] Aurora()
        {
            Color[] stops =
            [
                Color.FromArgb(25, 7, 26),     
                Color.FromArgb(0, 30, 60),     
                Color.FromArgb(0, 85, 80),     
                Color.FromArgb(0, 180, 150),   
                Color.FromArgb(0, 255, 120),   
                Color.FromArgb(120, 255, 180), 
                Color.FromArgb(200, 230, 255), 
                Color.FromArgb(180, 120, 255), 
                Color.FromArgb(80, 0, 60),     
            ];

            return InterpolateColor(stops);
        }

        private static Color[] InterpolateColor(Color[] stops, int steps = 280)
        {
            Color[] palette = new Color[steps];
            for (int i = 0; i < steps; i++)
            {
                double pos = (double)i / (steps - 1) * (stops.Length - 1);
                int idx = (int)pos;
                double frac = pos - idx;

                if (idx >= stops.Length - 1)
                {
                    palette[i] = stops[^1];
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
                double hue = (360.0 * i / steps) % 360;
                if (hue < 0) hue += 360;
                sat = Math.Clamp(sat, 0, 1);
                value = Math.Clamp(value, 0, 1);

                int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
                double f = hue / 60 - Math.Floor(hue / 60);

                value *= 255;
                int v = (int)value;
                int p = (int)(value * (1 - sat));
                int q = (int)(value * (1 - f * sat));
                int t = (int)(value * (1 - (1 - f) * sat));

                palette[i] = hi switch
                {
                    0 => Color.FromArgb(255, v, t, p),
                    1 => Color.FromArgb(255, q, v, p),
                    2 => Color.FromArgb(255, p, v, t),
                    3 => Color.FromArgb(255, p, q, v),
                    4 => Color.FromArgb(255, t, p, v),
                    _ => Color.FromArgb(255, v, p, q),
                };
            }

            return palette;
        }
    }
}
