using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Numerics;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.CPU;
using System;

namespace SteganoTool
{
    internal class ProcessFractal
    {
        private const double Epsilon = 1e-6;
        private const int MaxIterations = 200_000;
        private const double xMin = -1.5, yMin = -1.5;
        private const double xMax = 1.5, yMax = 1.5;

        private static readonly Complex[] RootBase =
        [
            new(1, 0),
            new(-0.5, Math.Sqrt(3)/2),
            new(-0.5, Math.Sqrt(3)/2)
        ];

        internal static Bitmap GenerateFractal(Complex c, int width, int height, double escapeRadius, string colorMethod,
            string fractalType, string vergeType)
        {
            int[] palette;
            int[,] roots = {};
            bool rootFillMethod = vergeType == "C";

            using var context = Context.CreateDefault();
            Accelerator accelerator;

            if (context.GetCudaDevices().Count != 0)
                accelerator = context.CreateCudaAccelerator(0);
            else if (context.GetCLDevices().Count != 0)
                accelerator = context.CreateCLAccelerator(0);
            else accelerator = context.CreateCPUAccelerator(0);

            var extent = new Index1D(width * height);
            using var setBuffer = accelerator.Allocate1D<double>(extent);
            using var colBuffer = accelerator.Allocate1D<int>(extent);

            Action<Index1D, ArrayView1D<double, Stride1D.Dense>, double, double, double, int, int, int> setKernel;
            Action<Index1D, ArrayView1D<double, Stride1D.Dense>, double, ArrayView1D<int, Stride1D.Dense>, 
                ArrayView1D<int, Stride1D.Dense>> palKernel;
            int whichFractal;
            switch (fractalType)
            {
                case "Julia":
                    setKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, double, double, int, int, int >(GPU.JuliaKernel);
                    whichFractal = 0;
                    palette = ColorChoiceD(colorMethod);
                    palKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense> >(GPU.ColorKernel);
                    break;
                case "Nova":
                    setKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, double, double, int, int, int>(GPU.JuliaKernel);
                    whichFractal = 1;
                    palette = ColorChoiceC(colorMethod);
                    palKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>>(GPU.ColorKernel);
                    break;
                case "Newton":
                    setKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, double, double, int, int, int>(GPU.JuliaKernel);
                    whichFractal = 2;
                    palette = ColorChoiceC(colorMethod);
                    palKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>>(GPU.ColorKernel);
                    break;
                default:
                    throw new ArgumentException("no selected Fractal type");
            }

            using var palBuffer = accelerator.Allocate1D(palette);

            setKernel(extent, setBuffer.View, c.Real, c.Imaginary, escapeRadius, width, height, whichFractal);
            accelerator.Synchronize();

            double[] iterations = setBuffer.GetAsArray1D();
            double maxIt = iterations.Max();

            palKernel(extent, setBuffer.View, maxIt, palBuffer.View, colBuffer.View);
            accelerator.Synchronize();

            int[] pixels = colBuffer.GetAsArray1D();

            Bitmap bmp = new (width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }

        private static double[,] GenerateDivergingSet(Complex c, int width, int height, double escapeRadius)
        {
            double[,] fractal = new double[width, height];

            Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 2 }, y =>
            {
                for (int x = 0; x < width; ++x)
                {
                    double zx = xMin + (xMax - xMin) * x / (width - 1);
                    double zy = yMin + (yMax - yMin) * y / (height - 1);
                    Complex z = new (zx, zy);
                    int iteration = 0;

                    while (iteration <= MaxIterations && z.Magnitude < escapeRadius)
                    {
                        z = c;
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

        private static (double[,] fractal, int[,] rootIdx) GenerateConvergingSet(Complex c, int width, int height)
        {
            double[,] fractal = new double[width, height];
            int[,] rootIdx = new int[width, height];

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; ++x)
                {
                    double zx = xMin + (xMax - xMin) * x / (width - 1);
                    double zy = yMin + (yMax - yMin) * y / (height - 1);
                    Complex z = new(zx, zy);
                    int iteration = 0;

                    while (iteration < MaxIterations && !HasConverged(z))
                    {
                        z = c;
                        iteration++;
                    }

                    double minDist = double.MaxValue;
                    for (int i = 0; i < RootBase.Length; ++i)
                    {
                        double dist = (z - RootBase[i]).Magnitude;
                        if (dist < minDist)
                        {
                            minDist = dist;
                            rootIdx[x, y] = i;
                        }
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

            return (fractal, rootIdx);
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
            for (int i = 0; i < pixelData.Length && i < totalBits; ++i)
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

            for (int i = 0; i < pixelData.Length && (dataLen == -1 || i < (dataLen + 4) * 8); ++i)
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

        private static bool HasConverged(Complex z)
        {
            foreach (var root in RootBase)
                if ((z - root).Magnitude < Epsilon)
                    return true;
            return false;
        }

        internal static bool CheckIterations(Complex c, int width, int height, double escapeRadius, int samples = 250)
        {
            Random rng = new ();
            double totalIterations = 0;
            int failures = 0;
            int maxFailures = (int)(samples * 0.05);

            for (int i = 0; i < samples; ++i)
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
                if (iteration == MaxIterations)
                    failures++;
            }

            double avgIterations = totalIterations / samples;

            return failures <= maxFailures && avgIterations < (double)(MaxIterations * 0.99999999999999999999999999999m);
        }

        private static int[] ColorChoiceD(string method)
        {
            return method switch
            {
                "Classic" => ClassicSet(),
                "Rainbow" => Rainbow(),
                "Aurora" => Aurora(),
                "Scientific" => ScientificVis(),
                _ => throw new ArgumentException("Invalid color method")
            };
        }

        private static int[] ColorChoiceC(string method)
        {
            return method switch
            {
                "RGB" => RGB(),
                "CYM" => CYM(),
                _ => throw new ArgumentException("Invalid color method")
            };
        }

        private static int[] ClassicSet()
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

        private static int[] Aurora()
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

        private static int[] InterpolateColor(Color[] stops, int steps = MaxIterations)
        {
            int[] palette = new int[steps + 1];
            for (int i = 0; i < steps; ++i)
            {
                double pos = (double)i / (steps - 1) * (stops.Length - 1);
                int idx = (int)pos;
                double frac = pos - idx;

                if (idx >= stops.Length - 1)
                {
                    palette[i] = stops[^1].ToArgb();
                }
                else
                {
                    Color c1 = stops[idx];
                    Color c2 = stops[idx + 1];
                    int r = (int)(c1.R + frac * (c2.R - c1.R));
                    int g = (int)(c1.G + frac * (c2.G - c1.G));
                    int b = (int)(c1.B + frac * (c2.B - c1.B));
                    palette[i] = Color.FromArgb(255, r, g, b).ToArgb();
                }
            }
            return palette;
        }

        private static int[] Rainbow(int steps = MaxIterations)
        {
            int[] palette = new int[steps + 1];
            for (int i = 0; i < steps; ++i)
            {
                double t = (double)i / (steps - 1);
                double r = Math.Sin(Math.PI * t);
                double g = Math.Sin(Math.PI * t + 2 * Math.PI / 3);
                double b = Math.Sin(Math.PI * t + 4 * Math.PI / 3);

                palette[i] = Color.FromArgb(
                    255,
                    (int)(127.5 * (r + 1)),
                    (int)(127.5 * (g + 1)),
                    (int)(127.5 * (b + 1))
                ).ToArgb();
            }
            return palette;
        }

        private static int[] ScientificVis(int steps = MaxIterations, double start = 0.5, double hue = 1.0, double rotations = -1.5,
            double gamma = 1.0)
        {
            int[] palette = new int[steps + 1];
            for (int i = 0; i < steps; ++i)
            {
                double t = (double)i / (steps - 1);
                double angle = 2 * Math.PI * (start / 3.0 + rotations * t);
                double fract = Math.Pow(t, gamma);
                double amp = hue * fract * (1 - fract) / 2;

                double r = fract + amp * (-0.14861 * Math.Cos(angle) + 1.78277 * Math.Sin(angle));
                double g = fract + amp * (-0.29227 * Math.Cos(angle) - 0.90649 * Math.Sin(angle));
                double b = fract + amp * (1.97294 * Math.Cos(angle));

                r = Math.Clamp(r, 0, 1);
                g = Math.Clamp(g, 0, 1);
                b = Math.Clamp(b, 0, 1);

                palette[i] = Color.FromArgb(
                    255,
                    (int)(255 * r),
                    (int)(255 * g),
                    (int)(255 * b)
                ).ToArgb();
            }
            return palette;
        }

        private static int[] RGB()
        {
            Color[] stops =
            [
                Color.Red,
                Color.Green,
                Color.Blue
            ];

            int[] palette = new int[stops.Length];

            for (int i = 0; i < stops.Length; i++)
            {
                palette[i] = stops[i].ToArgb();
            }

            return palette;
        }

        private static int[] CYM()
        {
            Color[] stops =
            [
                Color.Cyan,
                Color.Yellow,
                Color.Magenta
            ];

            int[] palette = new int[stops.Length];

            for (int i = 0; i < stops.Length; i++)
            {
                palette[i] = stops[i].ToArgb();
            }

            return palette;
        }

        private static Color BlendWithWhite(Color c, double t)
        {
            int r = (int)(c.R + (255 - c.R) * t);
            int g = (int)(c.G + (255 - c.G) * t);
            int b = (int)(c.B + (255 - c.B) * t);
            return Color.FromArgb(r, g, b);
        }
    }
}
