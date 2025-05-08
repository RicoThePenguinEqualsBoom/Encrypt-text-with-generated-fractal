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
        private const double xMin = -1.5, yMin = -1.5;
        private const double xMax = 1.5, yMax = 1.5;

        private static readonly Complex[] RootBase =
        [
            new(1, 0),
            new(-0.5, Math.Sqrt(3)/2),
            new(-0.5, -Math.Sqrt(3)/2)
        ];

        private delegate (Vector<double> zx, Vector<double> zy) VectorFunc(Vector<double> zx2, Vector<double> zy2, Vector<double> cReal, Vector<double> cImag);

        private static (Vector<double> zx, Vector<double> zy) JuliaFunc(Vector<double> zx2, Vector<double> zy2, Vector<double> cReal, Vector<double> cImag)
        {
            Vector<double> zx = (zx2 * zx2 - zy2 * zy2 + cReal);
            Vector<double> zy = (2 * zx2 * zy2 + cImag);
            return (zx, zy);
        }

        private static (Vector<double> zx, Vector<double> zy) NovaFunc(Vector<double> zx2, Vector<double> zy2, Vector<double> cReal, Vector<double> cImag)
        {
            return (zx2, zy2);
        }

        private static (Vector<double> zx, Vector<double> zy) NewtonFunc(Vector<double> zx2, Vector<double> zy2, Vector<double> cReal, Vector<double> cImag)
        {
            return (zx2, zy2);
        }

        private delegate (double zx, double zy) DoubleFunc(double zx2, double zy2, double cReal, double cImag);

        private static (double zx, double zy) JuliaFunc(double zx2, double zy2, double cReal, double cImag)
        {
            double zx = (zx2 * zx2 - zy2 * zy2 + cReal);
            double zy = (2 * zx2 * zy2 + cImag);
            return (zx, zy);
        }

        private static (double zx, double zy) NovaFunc(double zx2, double zy2, double cReal, double cImag)
        {
            return (zx2, zy2);
        }

        private static (double zx, double zy) NewtonFunc(double zx2, double zy2, double cReal, double cImag)
        {
            return (zx2, zy2);
        }

        internal static Bitmap GenerateFractal(Complex c, int width, int height, double escapeRadius, string colorMethod,
            string fractalType, string vergeType)
        {
            VectorFunc vFunc;
            DoubleFunc dFunc;
            Color[] palette;
            double[] fractal;
            int[,] roots = { };
            bool rootFillMethod = vergeType == "C";
            switch (fractalType)
            {
                case "Julia":
                    vFunc = JuliaFunc;
                    dFunc = JuliaFunc;
                    fractal = GenerateDivergingSet(c, width, height, escapeRadius, vFunc, dFunc);
                    palette = ColorChoiceD(colorMethod);
                    break;
                case "Nova":
                    vFunc = NovaFunc;
                    dFunc = NovaFunc;
                    (fractal, roots) = GenerateConvergingSet(c, width, height, dFunc);
                    palette = ColorChoiceC(colorMethod);
                    break;
                case "Newton":
                    vFunc = NewtonFunc;
                    dFunc = NewtonFunc;
                    (fractal, roots) = GenerateConvergingSet(c, width, height, dFunc);
                    palette = ColorChoiceC(colorMethod);
                    break;
                default:
                    throw new ArgumentException("no selected Fractal type");
            }

            Bitmap bmp = new(width, height);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            double maxVal = fractal.Cast<double>().Max();
            if (maxVal <= 0)
                maxVal = 1;
            int[] pixels = new int[width * height];

            int[] hist = new int[10];
            for (int i = 0; i < fractal.Length; ++i)
            {
                double v = fractal[i] / maxVal;
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                int b = (int)(v * hist.Length);
                if (b < 0) b = 0;
                if (b >= hist.Length) b = hist.Length - 1;
                hist[b]++;
            }
            MessageBox.Show(string.Join(", ", hist));

            try
            {
                if ((vFunc == NovaFunc || vFunc == NewtonFunc) && rootFillMethod)
                {
                    Parallel.For(0, height, y =>
                    {
                        int rowOffset = y * width;
                        for (int x = 0; x < width; ++x)
                        {
                            double norm = Math.Pow(fractal[rowOffset + x] / maxVal, 0.7f);
                            int colorIdx = roots[x, y] % palette.Length;
                            Color color = BlendWithWhite(palette[colorIdx], norm);
                            pixels[rowOffset + x] = color.ToArgb();
                        }
                    });
                }
                else
                {
                    Parallel.For(0, height, y =>
                    {
                        int rowOffset = y * width;
                        for (int x = 0; x < width; ++x)
                        {
                            double rawNorm = fractal[rowOffset + x] / maxVal;
                            if (double.IsNaN(rawNorm) || double.IsInfinity(rawNorm)) rawNorm = 0;
                            double norm = Math.Pow(Math.Max(rawNorm, 0), 0.7);
                            norm = Math.Min(norm, 1.0);
                            int colorIdx = (int)(norm * (palette.Length - 1));
                            Color color = palette[colorIdx];
                            pixels[rowOffset + x] = color.ToArgb();
                        }
                    });
                }
            }
            finally
            {
                IntPtr destPtr = 0;
                int srcOffset = 0;
                for (int y = 0; y < height; y++)
                {
                    destPtr = bmpData.Scan0 + y * bmpData.Stride;
                    srcOffset = y * width;
                    Marshal.Copy(pixels, srcOffset, destPtr, width);
                }
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }

        private static double[] GenerateDivergingSet(Complex c, int width, int height, double escapeRadius, VectorFunc vFunc,
            DoubleFunc dFunc)
        {
            double[] fractal = new double[width * height];
            double cReal = c.Real, cImag = c.Imaginary;

            /*Parallel.For(0, height, y =>
            {
                int rowOffset = y * width;
                double zy = yMin + (yMax - yMin) * y / (height - 1);
                for (int x = 0; x < width; ++x)
                {
                    double zx = xMin + (xMax - xMin) * x / (width - 1);
                    double zx2 = zx, zy2 = zy;
                    int iteration = 0;

                    double sqEscapeRadius = escapeRadius * escapeRadius;
                    while (iteration < MaxIterations && (zx2 * zx2 + zy2 * zy2) < sqEscapeRadius)
                    {
                        if (double.IsNaN(zx2) || double.IsNaN(zy2) || double.IsInfinity(zx2) || double.IsInfinity(zy2))
                            return;

                        (zx2, zy2) = func(zx2, zy2, cReal, cImag);
                        ++iteration;
                    }

                    double smoothValue;
                    if (iteration < MaxIterations)
                    {
                        double logZn = Math.Log(Math.Sqrt(zx2 * zx2 + zy2 * zy2)) / Math.Log(2);
                        double nu = Math.Log(logZn) / Math.Log(2);
                        smoothValue = iteration + 1 - nu;
                    }
                    else
                    {
                        smoothValue = MaxIterations;
                    }

                    if (double.IsNaN(smoothValue) || double.IsInfinity(smoothValue))
                        smoothValue = 0;

                    
                    fractal[rowOffset + x] = smoothValue;
                }
            });
            
            return fractal;

            double[] resultRow = new double[width];
            int simdWidth = Vector<double>.Count;
            double escapeSq = escapeRadius * escapeRadius;
            double dx = (xMax - xMin) / (width - 1);
            double dy = (yMax - yMin) / (height - 1);
            double[] zxArr = new double[simdWidth];
            var cRealVec = new Vector<double>(cReal);
            var cImagVec = new Vector<double>(cImag);
            var iterations = Vector<int>.Zero;

            Parallel.For(0, height, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) }, y =>
            {
                double zy = yMin + dy * y;

                for (int x = 0; x <= width - simdWidth; x += simdWidth)
                {
                    // Prepare SIMD batch of zx
                    for (int i = 0; i < simdWidth; ++i)
                        zxArr[i] = xMin + dx * (x + i);

                    var zx = new Vector<double>(zxArr);
                    var zyVec = new Vector<double>(zy);

                    var mask = Vector.GreaterThan(new Vector<double>(escapeSq), zx * zx + zyVec * zyVec);

                    var zx2 = zx;
                    var zy2 = zyVec;

                    for (int k = 0; k < MaxIterations; ++k)
                    {
                        var notEscaped = Vector.LessThan(zx2 * zx2 + zy2 * zy2, new Vector<double>(escapeSq));

                        if (Vector.EqualsAll((Vector<int>)notEscaped, Vector<int>.Zero) == true)
                            break;

                        (zx2, zy2) = vFunc(zx2, zy2, cRealVec, cImagVec);

                        // Increment iterations for not-escaped lanes
                        iterations += Vector.ConditionalSelect((Vector<int>)notEscaped, Vector<int>.One, Vector<int>.Zero);
                    }

                    // Store results
                    for (int i = 0; i < simdWidth; ++i)
                        resultRow[x + i] = iterations[i];
                }

                // Handle the tail (any remaining pixels)
                for (int x = 0; x < width; ++x)
                {
                    double zx = xMin + dx * x;
                    double zyVal = zy;
                    int iterations = 0;
                    while (iterations < MaxIterations && zx * zx + zyVal * zyVal < escapeSq)
                    {
                        double temp = zx * zx - zyVal * zyVal + cReal;
                        zyVal = 2 * zx * zyVal + cImag;
                        zx = temp;
                        ++iterations;
                    }
                    resultRow[x] = iterations;
                }

                for (int x = 0; x < width; ++x)
                    fractal[y * width + x] = resultRow[x];
            });

            return fractal;*/

            double log2 = Math.Log(2);
            double escapeRadiusSquared = escapeRadius * escapeRadius;
            int vectorSize = Vector<double>.Count;
            int lastSimdX = width - width % vectorSize;
            Vector<double> radiusSq = new(escapeRadiusSquared);
            Vector<double> cx = new(c.Real);
            Vector<double> cy = new(c.Imaginary);

            Parallel.For(0, height, y =>
            {
                double zy = yMin + (yMax - yMin) * y / (height - 1);
                Vector<double> zyVec = new(zy);
                int rowOffset = y * width;
                Span<double> zxArr = stackalloc double[vectorSize];

                int x = 0;
                for (; x <= width - vectorSize; x += vectorSize)
                {
                    for (int i = 0; i < vectorSize; i++)
                        zxArr[i] = xMin + (xMax - xMin) * (x + i) / (width - 1);

                    Vector<double> zx = new(zxArr);
                    Vector<double> zx2 = zx * zx;
                    Vector<double> zy2 = zyVec * zyVec;

                    Vector<int> iterVec = Vector<int>.Zero;

                    Vector<double> magSq = zx2 + zy2;
                    for (int iterations = 0; iterations < MaxIterations; ++iterations)
                    {
                        var mask = Vector.LessThan(magSq, radiusSq);
                        if (Vector.EqualsAll((Vector<int>)mask, Vector<int>.Zero))
                            break;

                        iterVec += Vector.ConditionalSelect((Vector<int>)mask, Vector<int>.One, Vector<int>.Zero);

                        (zx, zyVec) = vFunc(zx, zyVec, cx, cy);

                        zx2 = zx * zx;
                        zy2 = zyVec * zyVec;
                        magSq = zx2 + zy2;
                    }

                    for (int i = 0; i < vectorSize; i++)
                    {
                        double smooth;
                        if (iterVec[i] < MaxIterations)
                        {
                            double logZn = Math.Log(Math.Sqrt(zx2[i] + zy2[i])) / log2;
                            double nu = Math.Log(logZn) / log2;
                            smooth = iterVec[i] + 1 - nu;
                        }
                        else
                        {
                            smooth = MaxIterations;
                        }
                        
                        fractal[rowOffset + x + i] = smooth;
                    }
                }

                for (; x < width; ++x)
                {
                    double zx = xMin + (xMax - xMin) * x / (width - 1);
                    double zx2 = zx * zx;
                    double zy2 = zy * zy;
                    int iterations = 0;
                    while (iterations < MaxIterations && (zx2 + zy2) < escapeRadiusSquared)
                    {
                        (zx, zy) = dFunc(zx, zy, cReal, cImag);

                        zx2 = zx * zx;
                        zy2 = zy * zy;
                        ++iterations;
                    }

                    double smooth;
                    if (iterations < MaxIterations)
                    {
                        double logZn = Math.Log(Math.Sqrt(zx2 + zy2)) / log2;
                        double nu = Math.Log(logZn) / log2;
                        smooth = iterations + 1 - nu;
                    }
                    else
                    {
                        smooth = MaxIterations;
                    }

                    fractal[rowOffset + x] = smooth;
                }
            });

            return fractal;
        }

        private static (double[] fractal, int[,] rootIdx) GenerateConvergingSet(Complex c, int width, int height, DoubleFunc dFunc)
        {
            double[] fractal = new double[width * height];
            int[,] rootIdx = new int[width, height];
            double cReal = c.Real, cImag = c.Imaginary;

            Parallel.For(0, height, y =>
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; ++x)
                {
                    double zx = xMin + (xMax - xMin) * x / (width - 1);
                    double zy = yMin + (yMax - yMin) * y / (height - 1);
                    double zx2 = zx, zy2 = zy;
                    int iteration = 0;

                    while (iteration < MaxIterations && !HasConverged(zx2, zy2))
                    {
                        (zx2, zy2) = dFunc(zx2, zy2, cReal, cImag);
                        ++iteration;
                    }

                    double minDist = double.MaxValue;
                    for (int i = 0; i < RootBase.Length; ++i)
                    {
                        double dist = Math.Sqrt((double)((zx2 - RootBase[i].Real) * (zx2 - RootBase[i].Real) +
                            (zy2 - RootBase[i].Imaginary) * (zy2 - RootBase[i].Imaginary)));
                        if (dist < minDist)
                        {
                            minDist = dist;
                            rootIdx[x, y] = i;
                        }
                    }

                    double smoothValue;
                    if (iteration < MaxIterations)
                    {
                        double logZn = Math.Log(Math.Sqrt(zx2 * zx2 + zy2 * zy2)) / Math.Log(2);
                        double nu = Math.Log(logZn) / Math.Log(2);
                        smoothValue = iteration + 1 - nu;
                    }
                    else
                    {
                        smoothValue = MaxIterations;
                    }
                    fractal[rowOffset + x] = smoothValue;
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

        private static bool HasConverged(double zx, double zy)
        {
            foreach (var root in RootBase)
                if (Math.Sqrt((zx - root.Real) * (zx - root.Real) + (zy - root.Imaginary) * (zy - root.Imaginary)) < Epsilon)
                    return true;
            return false;
        }

        internal static bool CheckIterations(Complex c, int width, int height, double escapeRadius, int samplesPerAxis = 16)
        {
            Random rng = new ();
            double totalIterations = 0;
            int failures = 0;
            double cReal = c.Real, cImag = c.Imaginary;
            HashSet<int> uniqueIterations = [];

            int xStep = Math.Max(width / samplesPerAxis, 1);
            int yStep = Math.Max(height / samplesPerAxis, 1);
            int sampleCount = ((width + xStep - 1) / xStep) * ((height + yStep - 1) / yStep);
            int maxFailures = (int)(sampleCount * 0.1);

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
                        (zx2, zy2) = JuliaFunc(zx2, zy2, cReal, cImag);
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

            return uniqueIterations.Count > 5 && failures <= maxFailures && avgIterations < (MaxIterations * 0.9999999999999999);
        }

        private static Color[] ColorChoiceD(string method)
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

        private static Color[] ColorChoiceC(string method)
        {
            return method switch
            {
                "RGB" => RGB(),
                "CYM" => CYM(),
                _ => throw new ArgumentException("Invalid color method")
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

        private static Color[] InterpolateColor(Color[] stops, int steps = MaxIterations / 4)
        {
            Color[] palette = new Color[steps + 1];
            for (int i = 0; i <= steps; ++i)
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

        private static Color[] Rainbow(int steps = MaxIterations)
        {
            Color[] palette = new Color[steps + 1];
            for (int i = 0; i <= steps; ++i)
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
                );
            }
            return palette;
        }

        private static Color[] ScientificVis(int steps = MaxIterations, double start = 0.5, double hue = 1.0, double rotations = -1.5,
            double gamma = 1.0)
        {
            Color[] palette = new Color[steps + 1];
            for (int i = 0; i <= steps; ++i)
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
                );
            }
            return palette;
        }

        private static Color[] RGB()
        {
            return
            [
                Color.Red,
                Color.Green,
                Color.Blue
            ];
        }

        private static Color[] CYM()
        {
            return
            [
                Color.Cyan,
                Color.Yellow,
                Color.Magenta
            ];
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
