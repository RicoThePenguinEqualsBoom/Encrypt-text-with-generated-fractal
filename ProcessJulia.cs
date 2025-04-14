using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Threading.Tasks;
using System.Drawing;

namespace SteganoTool
{
    internal class ProcessJulia : IDisposable
    {
        private const double DefaultEscapeRadius = 2.0;
        private readonly Color[] colorPalette;

        /** internal static (Bitmap, string) Encrypt(int height, int width, int cryptL, string text)
         {
             string key = ProcessKey.Generate(cryptL);

             var keyParts = key.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
             double keyPart1 = double.Parse(keyParts[0]);
             double keyPart2 = double.Parse(keyParts[1]);

             Complex c = TextToComplex(text);

             Bitmap e = new Bitmap(width, height);

             return (e, key);
         }

         internal static Complex TextToComplex(string text)
         {
             double real = 0;
             double imaginary = 0;

             foreach (char c in text)
             {
                 real += c;
                 imaginary += c * 0.1;
             }

             real = (real % 2) - 1;
             imaginary = (imaginary % 2) - 1;

             return new Complex(real, imaginary);
         }**/

        internal ProcessJulia()
        {
            colorPalette = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                double t = i / 255.0;
                int r = (int)(255 * Math.Pow(t, 0.5));
                int g = (int)(255 * Math.Pow(t, 0.7));
                int b = (int)(255 * Math.Pow(t, 0.3));
                colorPalette[i] = Color.FromArgb(255, r, g, b);
            }
        }

        internal static Bitmap GenerateJulia(int width, int height, ProcessKey key)
        {
            var fractalData = GenerateSet(key.C, width, height, key.Iterations);
            Bitmap bmp = GenerateFractal(fractalData);
            return bmp;
        }

        private static double[,] GenerateSet(Complex c, int width, int height, int maxIterations)
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
                    while (iteration < maxIterations && Complex.Abs(z) < DefaultEscapeRadius)
                    {
                        z = Complex.Pow(z, 2) + c;
                        iteration++;
                    }

                    if (iteration < maxIterations)
                    {
                        double logZn = Math.Log(z.Magnitude) / Math.Log(2);
                        double nu = Math.Log(logZn) / Math.Log(2);
                        fractal[x, y] = iteration + 1 - nu;
                    }
                    else
                    {
                        fractal[x, y] = maxIterations;
                    }
                }
            });

            double maxVal = fractal.Cast<double>().Max();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    fractal[x, y] /= maxVal;
                }
            }

            return fractal;
        }

        private void CompareFractals(double[,] fractalData, int width, int height, ProcessKey key)
        {
            var generatedFractal = GenerateSet(key.C, width, height, key.Iterations);

            if (VerifyFractal(fractalData, generatedFractal) != true)
            {
                MessageBox.Show($"Fractal parameters : c = {key.C}, iteration = {key.Iterations}");
            }
        }

        private bool VerifyFractal(double[,] og, double[,] generated)
        {
            const double tolerance = 1e-6;
            int width = og.GetLength(0);
            int height = og.GetLength(1);

            for (int x = 0; x < width; x++)
            { 
                for (int y = 0; y < height; y++)
                {
                    if (Math.Abs(og[x, y] - generated[x, y]) > tolerance)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static Bitmap GenerateFractal(double[,] fractalData)
        {
            int width = fractalData.GetLength(0);
            int height = fractalData.GetLength(1);

            Bitmap bmp = new(width, height);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            int[] pixels = new int[width * height];

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    double value = fractalData[x, y];
                    int colorIndex = (int)(value * 255);
                    colorIndex = Math.Clamp(colorIndex, 0, 255);
                    Color color = new ProcessJulia().colorPalette[colorIndex];
                    pixels[y * width + x] = color.ToArgb();
                }
            });

            Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);

            bmp.UnlockBits(bmpData);

            return bmp;
        }

        internal static string DecryptFractal(Bitmap bmp, string keyString)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            double[,] fractalData = new double[width, height];

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            int[] pixels = new int[width * height];
            Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);

            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = Color.FromArgb(pixels[y * width + x]);

                    double normalizedValue = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
                    fractalData[x, y] = normalizedValue;
                }
            });

            bmp.UnlockBits(bmpData);

            var key = ProcessKey.FromString(keyString);

            var toKillOrNotToKill = new ProcessJulia();

            toKillOrNotToKill.CompareFractals(fractalData, width, height, key);

            string decryptedMessage = ProcessKey.Decrypt(key);

            return decryptedMessage;
        }

        private double[,] NormalizeFractalValues(double[,] fractal)
        {
            double maxVal = fractal.Cast<double>().Max();
            int width = fractal.GetLength(0);
            int height = fractal.GetLength(1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    fractal[x, y] /= maxVal;
                }
            }

            return fractal;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
