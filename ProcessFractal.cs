using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Numerics;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.CPU;
using System;
using ILGPU.IR.Types;

namespace SteganoTool
{
    internal class ProcessFractal
    {
        private const double Epsilon = 1e-6;
        private const int MaxIterations = 200_000;
        private const int ChunkSize = 1024;
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
            switch (fractalType)
            {
                case "Julia":
                    setKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, double, double, int, int, int >(GPU.JuliaKernel);
                    palette = ColorChoiceD(colorMethod);
                    palKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense> >(GPU.ColorKernel);
                    break;
                case "Nova":
                    setKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, double, double, int, int, int>(GPU.JuliaKernel);
                    palette = ColorChoiceC(colorMethod);
                    palKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, ArrayView1D<int, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>>(GPU.ColorKernel);
                    break;
                case "Newton":
                    setKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                        double, double, double, int, int, int>(GPU.JuliaKernel);
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

            int numChunks = (extent + ChunkSize - 1) / ChunkSize;

            using var partialMax = accelerator.Allocate1D<double>(numChunks);

            var redKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<double, Stride1D.Dense>,
                ArrayView1D<double, Stride1D.Dense>, int >(GPU.ReductionKernel);

            redKernel(numChunks, setBuffer.View, partialMax.View, extent);
            accelerator.Synchronize();

            double maxIt = partialMax.GetAsArray1D().Max();

            palKernel(extent, setBuffer.View, maxIt, palBuffer.View, colBuffer.View);
            accelerator.Synchronize();

            int[] pixels = colBuffer.GetAsArray1D();

            Bitmap bmp = new (width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            unsafe
            {
                try
                {
                    var destBytes = new Span<byte>((void*)bmpData.Scan0, pixels.Length * sizeof(int));

                    var srcBytes = MemoryMarshal.AsBytes<int>(pixels);

                    srcBytes.CopyTo(destBytes);
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }
            }

            return bmp;
        }

        internal static Bitmap EmbedLSB(Bitmap bmp, byte[] data)
        {
            int width = bmp.Width, height = bmp.Height;

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
                if (Math.Sqrt((zx - root.Real) * (zx - root.Real) + (zy - root.Imaginary) * (zy - root.Imaginary)) < Epsilon)
                    return true;
            return false;
        }

        internal static bool CheckIterations(Complex c, int width, int height, double escapeRadius, string fractalType, int samplesPerAxis = 32)
        {
            Random rng = new ();
            double totalIterations = 0;
            int failures = 0;
            double cReal = c.Real, cImag = c.Imaginary;
            HashSet<int> uniqueIterations = [];

            int xStep = XMath.Max(width / samplesPerAxis, 1);
            int yStep = XMath.Max(height / samplesPerAxis, 1);
            int sampleCount = ((width + xStep - 1) / xStep) * ((height + yStep - 1) / yStep);
            int maxFailures = (int)(sampleCount * 0.004);
            int minUnIterations = (int)(sampleCount * 0.022);

            object lockObj = new();

            Parallel.For(0, height / yStep, gridYidx =>
            {
                int y = gridYidx * yStep;
                double zy = yMin + (yMax - yMin) * y / (height - 1);
                for (int x = 0; x < width; x += xStep)
                {
                    double zx = xMin + (xMax - xMin) * x / (width - 1);
                    double zx2 = zx, zy2 = zy;
                    int iteration = 0;

                    while (iteration < MaxIterations && (zx2 * zx2 + zy2 * zy2) < escapeRadius * escapeRadius)
                    {
                        if (double.IsNaN(zx2) || double.IsNaN(zy2) || double.IsInfinity(zx2) || double.IsInfinity(zy2))
                            return;
                        (zx2, zy2) = func(zx2, zy2, cReal, cImag);
                        ++iteration;
                    }

                    lock (lockObj)
                    {
                        totalIterations += iteration;
                        uniqueIterations.Add(iteration);
                        if (iteration == MaxIterations)
                            ++failures;
                    }
                }
            });

            double avgIterations = totalIterations / sampleCount;

            return uniqueIterations.Count >= minUnIterations && failures <= maxFailures &&
                avgIterations < (MaxIterations * 0.9999999999999999);
        }

        private static int[] ColorChoiceD(string method)
        {
            return method switch
            {
                "Classic" => ClassicSet(),
                "B&W" => BW(),
                "Nuclear" => Nuclear(),
                "LSD" => RainbowLSD(),
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
                Color.FromArgb(20, 20, 20)
            ];

            return InterpolateColor(stops);
        }

        private static Color[] BW()
        {
            Color[] stops =
            [
                Color.White,
                Color.Black
            ];

            return InterpolateColor(stops);
        }

        private static Color[] Nuclear()
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

            double stepRange = steps - 1;
            double stopRange = stops.Length - 1;

            for (int i = 0; i < steps; ++i)
            {
                double pos = steps > 1 ? i / stepRange * stopRange: 0;
                int idx = Math.Min((int)pos, stops.Length - 2);
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

        private static Color[] RainbowLSD(int steps = MaxIterations, double frequency = 50.0)
        {
            Color[] palette = new Color[steps + 1];
            for (int i = 0; i <= steps; ++i)
            {
                double t = (double)i / (steps - 1);
                double r = Math.Sin(frequency * Math.PI * t);
                double g = Math.Sin(frequency * Math.PI * t + 2 * Math.PI / 3);
                double b = Math.Sin(frequency * Math.PI * t + 4 * Math.PI / 3);

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
            return Color.FromArgb(r, g, b).ToArgb();
        }
    }
}
