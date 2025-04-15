using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Threading.Tasks;
using System.Drawing;
using System.CodeDom.Compiler;
using System.Text;

namespace SteganoTool
{
    internal class ProcessJulia
    {
        private const int MaxIterations = 300;
        private const double Zoom = 1.5;
        private const double EscapeRadius = 4.0;
        private const double ColorOffset = 240.0;
        private const double SaturationBase = 0.8;
        private const double SaturationRange = 0.2;
        private const double ValueMultiplier = 1.5;

        internal static Bitmap GenerateJulia(ProcessKey fullKey, int width, int height, string encryptedText)
        {
            var (lengthBits, messageBits) = ProcessKey.TextToBits(encryptedText);

            var allBits = lengthBits.Concat(messageBits).ToArray();

            var bmp = new Bitmap(width, height);

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb);

            double scaleX = Zoom * 2.0 / width;
            double scaleY = Zoom * 2.0 / height;

            Parallel.For(0, height, y =>
            {
                var row = new byte[width * 4];

                for (int x = 0; x < width; x++)
                {
                    var point = new Complex(
                        (x - width / 2) * scaleX,
                        (y - height / 2) * scaleY
                    );

                    var (iterations, magnitude) = CalculateJuliaPoint(point, new Complex(fullKey.RealC, fullKey.ImaginaryC));
                    var color = GetColor(iterations, magnitude);

                    int pixelIndex = y * width + x;
                    int offset = x * 4;

                    row[offset] = color.B;
                    row[offset + 1] = color.G;
                    row[offset + 2] = pixelIndex < allBits.Length ? EmbedBit(color.R, allBits[pixelIndex]) : color.R;
                    row[offset + 3] = color.A;
                }

                Marshal.Copy(row, 0, IntPtr.Add(bmpData.Scan0, y * bmpData.Stride), row.Length);
            });

            bmp.UnlockBits(bmpData);
            return bmp;
        }

        internal static string DecodeJulia(Bitmap bmp)
        {
            var width = bmp.Width;
            var height = bmp.Height;
            var bits = new List<bool>();

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            var lengthBits = new bool[32];
            var row = new byte[bmpData.Stride];

            for (int i = 0; i < 32; i++)
            {
                int x = i % width;
                int y = i / width;

                if (y >= height)
                {
                    throw new ArgumentException($"Image is too small to decode message: {width}x{height}");
                }

                if (x == 0)
                {
                    Marshal.Copy(IntPtr.Add(bmpData.Scan0, y * bmpData.Stride), row, 0, bmpData.Stride);
                }

                lengthBits[i] = ExtractBit(row[x * 4 + 2]);
            }

            var length = BitsToInt(lengthBits);

            if (length < 0 || length > (width * height - 32) / 8)
            {
               throw new InvalidOperationException($"Invalid length of message: {length} must be between 0 and {(width * height - 32) / 8} ");
            }

            var messageBits = new bool[length * 8];

            for (int i = 0; i < length * 8; i++)
            {
                int pos = i + 32;
                int x = pos % width;
                int y = pos / width;

                if (y >= height)
                {
                    throw new ArgumentException("image data is truncated");
                }

                if (x == 0)
                {
                    Marshal.Copy(IntPtr.Add(bmpData.Scan0, y * bmpData.Stride), row, 0, bmpData.Stride);
                }

                messageBits[i] = ExtractBit(row[x * 4 + 2]);
            }



            try
            {
                var encryptedText = BitsToText(bits.ToArray());
                var testDecode = Convert.FromBase64String(BitsToText(bits.ToArray()));
                return encryptedText;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to decode message: {ex.Message}");
            }


        }

        private static (int, double) CalculateJuliaPoint(Complex z, Complex c)
        {
            int iterations = 0;
            double zReal = z.Real;
            double zImag = z.Imaginary;
            double cReal = c.Real;
            double cImag = c.Imaginary;

            while (iterations < MaxIterations)
            {
                double r2 = zReal * zReal;
                double i2 = zImag * zImag;

                if (r2 + i2 > EscapeRadius)
                    break;

                zImag = 2.0 * zReal * zImag + cImag;
                zReal = r2 - i2 + cReal;

                iterations++;
            }

            var magnitude = Math.Sqrt(zReal * zReal + zImag * zImag);

            return (iterations, magnitude);
        }

        private static Color GetColor(int iterations, double magnitude)
        {
            if (iterations == MaxIterations)
                return Color.Black;

            double smoothed = iterations + 1 - Math.Log(Math.Log(magnitude)) / Math.Log(2.0);
            smoothed = smoothed / MaxIterations;

            return ColorFromHSV(
                (smoothed * 360 + ColorOffset) % 360,
                SaturationBase + smoothed * SaturationRange,
                iterations < MaxIterations ? Math.Min(1.0, smoothed * ValueMultiplier) : 0
            );
        }

        private static Color ColorFromHSV(double hue, double saturation, double value)
        {
            hue = Math.Clamp(hue, 0, 360);
            saturation = Math.Clamp(saturation, 0, 1);
            value = Math.Clamp(value, 0, 1);

            double c = value * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = value - c;

            double r, g, b;

            switch (hue)
            {
                case < 60:
                    r = c; g = x; b = 0;
                    break;
                case < 120:
                    r = x; g = c; b = 0;
                    break;
                case < 180:
                    r = 0; g = c; b = x;
                    break;
                case < 240:
                    r = 0; g = x; b = c;
                    break;
                case < 300:
                    r = x; g = 0; b = c;
                    break;
                default:
                    r = c; g = 0; b = x;
                    break;
            }

            return Color.FromArgb(
                255,
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255)
            );
        }

        private static byte EmbedBit(byte value, bool bit)
        {
            return bit ? (byte)(value | 1) : (byte)(value & ~1);
        }

        private static bool ExtractBit(byte value)
        {
            return (value & 1) == 1;
        }

        public static int BitsToInt(bool[] bits)
        {
            if (bits == null || bits.Length != 32)
            {
                throw new ArgumentException($"Invalid bit array length: {bits?.Length}");
            }

            int value = 0;
            for (int i = 0; i < 32; i++)
            {
                if (bits[i])
                {
                    value |= 1 << (31 - i);
                }
            }

            if (value < 0 || value > 1_000_000)
            {
                throw new ArgumentException($"Invalid integer value: {value}");
            }

            return value;
        }

        private static string BitsToText(bool[] bits)
        {
            var bytes = new byte[bits.Length / 8];
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = 0;
                for (int j = 0; j < 8; j++)
                {
                    if (bits[i * 8 + j])
                    {
                        b |= (byte)(1 << (7 - j));
                    }
                }
                bytes[i] = b;
            }

            return Convert.ToBase64String(bytes);
        }
    }
}
