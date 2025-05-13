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
using ILGPU.IR.Types;
using System.Diagnostics;

namespace SteganoTool
{
    /**Things leftover from trying to implement other fractal types but that won't be removed to facilitate future
     * implementation will henceforth be labeled as Leftovers**/
    /*Most values are set as doubles for precision(decimals would be a computational nightmare)
     * but floats also return valid results and are faster*/
    internal class ProcessFractal
    {
        //Declare the constants
        private const double Epsilon = 1e-6;
        private const int MaxIterations = 200_000;
        private const double xMin = -1.5, yMin = -1.5;
        private const double xMax = 1.5, yMax = 1.5;
        private static readonly IReadOnlyList<int> TileSizes = [128, 256, 512, 1_024, 2_048, 4_096, 8_192, 16_384, 32_768];

        //Leftovers
        private static readonly Complex[] RootBase =
        [
            new(1, 0),
            new(-0.5, Math.Sqrt(3)/2),
            new(-0.5, -Math.Sqrt(3)/2)
        ];

        //Create delegates for the math required by the fractal generation methods
        private delegate (Vector<double> zx, Vector<double> zy) VectorFunc(Vector<double> zx2, Vector<double> zy2,
            Vector<double> cReal, Vector<double> cImag);

        private static (Vector<double> zx, Vector<double> zy) JuliaFunc(Vector<double> zx2, Vector<double> zy2,
            Vector<double> cReal, Vector<double> cImag)
        {
            Vector<double> zx = (zx2 * zx2 - zy2 * zy2 + cReal);
            Vector<double> zy = (2 * zx2 * zy2 + cImag);
            return (zx, zy);
        }

        private static (Vector<double> zx, Vector<double> zy) NovaFunc(Vector<double> zx2, Vector<double> zy2,
            Vector<double> cReal, Vector<double> cImag)
        {
            return (zx2, zy2);
        }

        private static (Vector<double> zx, Vector<double> zy) NewtonFunc(Vector<double> zx2, Vector<double> zy2,
            Vector<double> cReal, Vector<double> cImag)
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

        //Generate the fractal
        internal static Bitmap GenerateFractal(Complex c, int width, int height, double escapeRadius, string colorMethod,
            string fractalType, string vergeType)
        {
            //Declare your variables
            VectorFunc vFunc;
            DoubleFunc dFunc;
            Color[] palette;
            double[] fractal;
            int[,] roots = { };
            bool rootFillMethod = vergeType == "C";
            //Leftovers
            switch (fractalType)
            {
                case "Julia":
                    //Grab the function delegates belonging to the selected fractal method
                    vFunc = JuliaFunc;
                    dFunc = JuliaFunc;
                    //Generate the fractal set
                    fractal = GenerateDivergingSet(c, width, height, escapeRadius, vFunc, dFunc);
                    //Grab the selected color palette
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

            //Create the image with the appropriate dimensions
            Bitmap bmp = new(width, height);
            //Lock the image data for modification
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            //Get the largest value in the fractal set for color normalization
            double maxVal = fractal.Cast<double>().Max();
            if (maxVal <= 0)
                maxVal = 1;
            //Create the pixel array for coloring
            int[] pixels = new int[width * height];

            try
            {
                //Leftovers
                if ((vFunc == NovaFunc || vFunc == NewtonFunc) && rootFillMethod)
                {
                    Parallel.For(0, height, y =>
                    {
                        int rowOffset = y * width;
                        for (int x = 0; x < width; ++x)
                        {
                            double norm = Math.Pow(fractal[rowOffset + x] / maxVal, 0.65f);
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
                        //Precompute the offset for each row to avoid repeated math
                        int rowOffset = y * width;
                        for (int x = 0; x < width; ++x)
                        {
                            //Normalize the fractal value and clamp it to the palette size(I use an arbitrary value of 0.7
                            //but experimentation is encouraged)
                            double norm = Math.Pow(fractal[rowOffset + x] / maxVal, 0.65f);
                            //Make sure norm is a valid value
                            if (double.IsNaN(norm) || double.IsInfinity(norm) || norm < 0) norm = 0;
                            else if (norm > 1) norm = 1;
                            //Scale the norm to the palette size
                            norm *= (palette.Length - 1);
                            int colorIdx = (int)Math.Clamp(norm, 0, palette.Length - 1);
                            //Assign the color values
                            Color color = palette[colorIdx];
                            pixels[rowOffset + x] = color.ToArgb();
                        }
                    });
                }
            }
            finally
            {
                //Copy the color values to the image data and unlock it
                Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }

        private static double[] GenerateDivergingSet(Complex c, int width, int height, double escapeRadius, VectorFunc vFunc,
            DoubleFunc dFunc)
        {
            //Declare your variables
            double[] fractal = new double[width * height];
            double cReal = c.Real, cImag = c.Imaginary;
            double log2 = Math.Log(2);
            double escapeRadiusSquared = escapeRadius * escapeRadius;
            int vectorSize = Vector<double>.Count;
            int lastSimdX = width - width % vectorSize;
            Vector<double> radiusSq = new(escapeRadiusSquared);
            Vector<double> cx = new(c.Real);
            Vector<double> cy = new(c.Imaginary);

            //Create the tiles required for optimal parallelization (I used a list of powers of 2 for optimal cpu compatability)
            double pTileSize = Math.Sqrt(width * height / 4);
            int rTileSize = TileSizes.Aggregate((x, y) => Math.Abs(x - pTileSize) < Math.Abs(y - pTileSize) ? x : y);
            var tiles = new List<(int x0, int y0, int x1, int y1)>();
            for (int y = 0; y < height; y += rTileSize)
                for (int x = 0; x < width; x += rTileSize)
                    tiles.Add((x, y, Math.Min(x + rTileSize, width), Math.Min(y + rTileSize, height)));

            Parallel.ForEach(tiles, tile =>
            {
                Parallel.For(tile.y0, tile.y1, y =>
                {
                    //Precompute available variables to avoid repeated math
                    Span<double> zxArr = stackalloc double[vectorSize];
                    double zy = yMin + (yMax - yMin) * y / (height - 1);
                    int rowOffset = y * width;

                    //Declare the size variable for the SIMD loop outside to avoid doing unnecessary math for the tail
                    int x = tile.x0;
                    for (; x <= tile.x1 - vectorSize; x += vectorSize)
                    {
                        //Compute the zx values for the SIMD vector size
                        for (int i = 0; i < vectorSize; ++i)
                            zxArr[i] = xMin + (xMax - xMin) * (x + i) / (width - 1);

                        //Declare the remaining SIMD variables
                        Vector<double> zxVec = new(zxArr);
                        Vector<double> zx2 = zxVec * zxVec;
                        Vector<double> zyVec = new(zy);
                        Vector<double> zy2 = zyVec * zyVec;
                        Vector<double> lastZx = zxVec;
                        Vector<double> lastZy = zyVec;

                        Vector<long> iterVec = Vector<long>.Zero;
                        Vector<long> escapedMask = Vector<long>.Zero;

                        Vector<double> magSq = zx2 + zy2;
                        //Loop for the iterations to reach the max iterations or escape radius
                        for (int iterations = 0; iterations < MaxIterations; ++iterations)
                        {
                            //Get the zx and zy values that are within the escape radius
                            var mask = Vector.LessThan(magSq, radiusSq) & ~escapedMask;

                            //Get the zx and zy values that are outside the escape radius
                            var newlyEscaped = Vector.GreaterThanOrEqual(magSq, radiusSq) & ~escapedMask;

                            //Check wether the previous values are still within the escape radius
                            lastZx = Vector.ConditionalSelect(newlyEscaped, zxVec, lastZx);
                            lastZy = Vector.ConditionalSelect(newlyEscaped, zyVec, lastZy);

                            //Increment the escaped count for the values that aren't still within the escape radius
                            escapedMask |= newlyEscaped;

                            //Increment the iteration count for the values that are still within the escape radius
                            iterVec += Vector.ConditionalSelect(mask, Vector<long>.One, Vector<long>.Zero);

                            //Do the fractal math
                            (Vector<double> zxNew, Vector<double> zyNew) = vFunc(zxVec, zyVec, cx, cy);

                            //Check wether the new values are still within the escape radius
                            zxVec = Vector.ConditionalSelect(mask, zxNew, zxVec);
                            zyVec = Vector.ConditionalSelect(mask, zyNew, zyVec);

                            //Leave the loop if all values are outside the escape radius
                            if (Vector.EqualsAll(mask, Vector<long>.Zero))
                                break;

                            magSq = zxNew * zxNew + zyNew * zyNew;
                        }

                        for (int i = 0; i < vectorSize; ++i)
                        {
                            double smooth;
                            if (iterVec[i] < MaxIterations)
                            {
                                //Smooth out the values that aren't at the max iterations for clean coloring
                                double mag = Math.Sqrt(lastZy[i] * lastZy[i] + lastZx[i] * lastZx[i]);
                                if (mag > 0)
                                {
                                    double logZn = Math.Log(mag) / log2;
                                    double nu = Math.Log(logZn) / log2;
                                    smooth = iterVec[i] + 1 - nu;
                                }
                                else
                                {
                                    smooth = iterVec[i];
                                }
                            }
                            else
                            {
                                smooth = MaxIterations;
                            }

                            fractal[rowOffset + x + i] = smooth;
                        }
                    }

                    //Take care of the tail from the SIMD loop (same math only without the complicated vectors and stuff)
                    for (; x < tile.x1; ++x)
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
            });

            return fractal;
        }

        //Leftovers
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
            //Get the target image' dimensions
            int width = bmp.Width, height = bmp.Height;

            //Get all the data to be embedded in the correct formats
            byte[] lengthBytes = BitConverter.GetBytes(data.Length);
            byte[] fullData = [.. lengthBytes, .. data];

            //Create a new bitmap to avoid errors from modifying the original
            Bitmap encrypted = new (bmp);

            //Lock the new image' data for modification
            BitmapData bmpData = encrypted.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite,
                PixelFormat.Format24bppRgb);

            //Get the stride and size of the image data to create the modifying array
            int stride = bmpData.Stride;
            int bytes = stride * height;
            byte[] pixelData = new byte[bytes];

            //Copy the image data to the modifying array
            Marshal.Copy(bmpData.Scan0, pixelData, 0, bytes);

            //Get the total number of bits to be embedded
            int totalBits = fullData.Length * 8;
            for (int i = 0; i < pixelData.Length && i < totalBits; ++i)
            {
                //Using the least significant bit of each pixel, embed the data
                int byteIdx = i / 8;
                int bitIdx = 7 - (i % 8);
                int bit = (fullData[byteIdx] >> bitIdx) & 1;
                pixelData[i] = (byte)((pixelData[i] & 0xFE) | bit);
            }

            //Copy the modified data back to the image and unlock it
            Marshal.Copy(pixelData, 0, bmpData.Scan0, bytes);
            encrypted.UnlockBits(bmpData);

            return encrypted;
        }

        internal static byte[] ExtractLSB(Bitmap bmp)
        {
            //Get the target image' dimensions
            int width = bmp.Width;
            int height = bmp.Height;
            //Get the maximum number of bytes that can be extracted
            int maxBytes = width * height * 3 / 8;

            //Lock the image data for reading
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            //Get the stride and size of the image data to create the reading array
            int stride = bmpData.Stride;
            int bytes = stride * height;
            byte[] pixelData = new byte[bytes];

            //Copy the image data to the reading array
            Marshal.Copy(bmpData.Scan0, pixelData, 0, bytes);

            //Create a buffer to store the extracted data
            byte[] buffer = new byte[maxBytes];
            int dataLen = -1;

            for (int i = 0; i < pixelData.Length && (dataLen == -1 || i < (dataLen + 4) * 8); ++i)
            {
                //Using the least significant bit of each pixel, extract the data
                int byteIdx = i / 8;
                int bitIdx = 7 - (i % 8);
                int bit = pixelData[i] & 1;
                buffer[byteIdx] = (byte)((buffer[byteIdx] & ~(1 << bitIdx)) | (bit << bitIdx));

                //Check if the data length has been extracted
                if (i == 32 && dataLen == -1)
                {
                    dataLen = BitConverter.ToInt32(buffer, 0);
                }
            }

            //Unlock the image data
            bmp.UnlockBits(bmpData);

            if (dataLen < 0 || dataLen > maxBytes - 4)
                throw new InvalidOperationException("Invalid or no embedded data found");

            //Copy the extracted data to a new array
            byte[] result = new byte[dataLen];
            Array.Copy(buffer, 4, result, 0, dataLen);

            return result;
        }

        //Leftovers
        private static bool HasConverged(double zx, double zy)
        {
            foreach (var root in RootBase)
                if (Math.Sqrt((zx - root.Real) * (zx - root.Real) + (zy - root.Imaginary) * (zy - root.Imaginary)) < Epsilon)
                    return true;
            return false;
        }

        internal static bool CheckIterations(Complex c, int width, int height, double escapeRadius, string fractalType,
            int samplesPerAxis = 32)
        {
            //Declare your variables
            Random rng = new ();
            double totalIterations = 0;
            int failures = 0;
            double cReal = c.Real, cImag = c.Imaginary;
            HashSet<int> uniqueIterations = [];
            DoubleFunc func = fractalType switch
            {
                "Julia" => JuliaFunc,
                "Nova" => NovaFunc,
                "Newton" => NewtonFunc,
                _ => JuliaFunc
            };

            //We only need to check a sample of the values to get an idea of the fractal, so declare the values for that 
            int xStep = Math.Max(width / samplesPerAxis, 1);
            int yStep = Math.Max(height / samplesPerAxis, 1);
            //Get the number of samples to be taken and based on that calculate the failure parameters
            int sampleCount = ((width + xStep - 1) / xStep) * ((height + yStep - 1) / yStep);
            //Reducing the amount of failures allowed reduces the chance of solid "blobs" of never escaping iterations
            int maxFailures = (int)(sampleCount * 0.004);
            //Increasing the amount of unique iterations required decreases the amount of "simple" fractals
            int minUnIterations = (int)(sampleCount * 0.022);

            object lockObj = new();

            //Use the same methods as the fractal generation
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

            //Calculate the average iterations and check if the fractal is valid
            double avgIterations = totalIterations / sampleCount;

            return uniqueIterations.Count >= minUnIterations && failures <= maxFailures &&
                avgIterations < (MaxIterations * 0.9999999999999999);
        }

        private static Color[] ColorChoiceD(string method)
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

        //Leftovers
        private static Color[] ColorChoiceC(string method)
        {
            return method switch
            {
                "RGB" => RGB(),
                "CYM" => CYM(),
                _ => throw new ArgumentException("Invalid color method")
            };
        }

        //Create your color palettes
        private static Color[] ClassicSet()
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

        private static Color[] InterpolateColor(Color[] stops, int steps = MaxIterations, double gamma = 2.2)
        {
            //Create a palette with a number of steps associated to the max iterations
            Color[] palette = new Color[steps + 1];

            //Precompute the required variables
            double stepRange = steps - 1;
            double stopRange = stops.Length - 1;

            for (int i = 0; i < steps; ++i)
            {
                //Get the values for the interpolation
                double pos = steps > 1 ? i / stepRange * stopRange: 0;
                int idx = Math.Min((int)pos, stops.Length - 2);
                double frac = pos - idx;

                if (idx >= stops.Length - 1)
                {
                    palette[i] = stops[^1];
                }
                else
                {
                    //Interpolate the color values
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

        //Sinewave based rainbow palette
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
                );
            }
            return palette;
        }

        //Lightly modified Cubehelix color scheme method
        private static Color[] ScientificVis(int steps = MaxIterations, double start = 0.7, double hue = 1.0, double rotations = -1.5,
            double gamma = 1.2)
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

        //Leftovers
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
