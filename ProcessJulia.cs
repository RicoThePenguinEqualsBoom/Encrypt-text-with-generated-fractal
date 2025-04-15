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

        internal static Bitmap GenerateJulia(ProcessKey fullKey, int width, int height, string encryptedText)
        {
            var (lengthBits, messageBits) = ProcessKey.TextToBits(encryptedText);

            var allBits = lengthBits.Concat(messageBits).ToArray();

            var bmp = new Bitmap(width, height);
            var bitIndex = 0;

            double scaleX = Zoom * 2.0 / width;
            double scaleY = Zoom * 2.0 / height;

            for (int i = 0; i < 32; i++)
            {
                int x = i % width;
                int y = i / width;

                var point = new Complex(
                    (x - width / 2) * scaleX,
                    (y - height / 2) * scaleY
                );

                var iterations = CalculateJuliaPoint(point, new Complex(fullKey.RealC, fullKey.ImaginaryC));
                var color = GetColor(iterations);

                color = EmbedBit(color, lengthBits[i]);
                bmp.SetPixel(x, y, color);
            }

            var remainingBits = allBits.Skip(32).ToArray();
            Parallel.For(32, width * height, i =>
            {
                if (i - 32 >= remainingBits.Length) return;

                int x = i % width;
                int y = i / width;

                var point = new Complex(
                    (x - width / 2) * scaleX,
                    (y - height / 2) * scaleY
                );

                var iterations = CalculateJuliaPoint(point, new Complex(fullKey.RealC, fullKey.ImaginaryC));
                var color = GetColor(iterations);
                color = EmbedBit(color, remainingBits[i - 32]);

                lock (bmp)
                {
                    bmp.SetPixel(x, y, color);
                }
            });

            return bmp;
        }

        internal static string DecodeJulia(Bitmap bmp)
        {
            var width = bmp.Width;
            var height = bmp.Height;
            var bits = new List<bool>();

            for (int i = 0; i < 32; i++)
            {
                int x = i % width;
                int y = i / width;

                if (y >= height)
                {
                    throw new ArgumentException($"Image is too small to decode message: {width}x{height}");
                }

                var color = bmp.GetPixel(x, y);
                bool bit = ExtractBit(color);
                bits.Add(bit);

                MessageBox.Show($"position ({x}, {y}): R={color.R}, bit={bit}");
            }

            MessageBox.Show("length bits read: " + string.Join("", bits.Take(32).Select(b => b ? "1" : "0")));

            var lengthBits = bits.ToArray();
            var length = BitsToInt(lengthBits);
            MessageBox.Show($"length: {length}");

            if (length < 0 || length > (width * height - 32) / 8)
            {
               throw new InvalidOperationException($"Invalid length of message: {length} must be between 0 and {(width * height - 32) / 8} ");
            }

            bits.Clear();
            int totalBitsNeeded = length * 8;

            for (int i = 32; i < totalBitsNeeded; i++)
            {
                int pos = i + 32;
                int x = pos % width;
                int y = pos / width;

                if (y >= height)
                {
                    throw new ArgumentException("image data is truncated");
                }

                var color = bmp.GetPixel(x, y);
                bits.Add(ExtractBit(color));
            }


            if (bits.Count != length * 8)
            {
                throw new InvalidOperationException($"Not enough bits to decode message: {bits.Count} < {length * 8}");
            }

            try
            {
                var testDecode = Convert.FromBase64String(BitsToText(bits.ToArray()));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to decode message: {ex.Message}");
            }

            var encryptedText = BitsToText(bits.ToArray());

            return encryptedText;
        }

        private static int CalculateJuliaPoint(Complex z, Complex c)
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

                if (r2 + i2 > 4.0)
                    break;

                zImag = 2.0 * zReal * zImag + cImag;
                zReal = r2 - i2 + cReal;

                iterations++;
            }

            return iterations;
        }

        private static Color GetColor(int iterations)
        {
            if (iterations == MaxIterations)
                return Color.Black;

            double smoothed = iterations + 1 - Math.Log(Math.Log(2.0)) / Math.Log(2.0);
            smoothed = smoothed / MaxIterations;

            return ColorFromHSV(
                (smoothed * 360) % 360,
                0.8,
                iterations < MaxIterations ? Math.Min(1.0, smoothed * 1.5) : 0
            );
        }

        private static Color ColorFromHSV(double hue, double saturation, double value)
        {
            hue = Math.Clamp(hue, 0, 360);
            saturation = Math.Clamp(saturation, 0, 1);
            value = Math.Clamp(value, 0, 1);

            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            double v = value;
            double p = value * (1 - saturation);
            double q = value * (1 - f * saturation);
            double t = value * (1 - (1 - f) * saturation);

            switch (hi)
            {
                case 0: return Color.FromArgb((int)v, (int)t, (int)p);
                case 1: return Color.FromArgb((int)q, (int)v, (int)p);
                case 2: return Color.FromArgb((int)p, (int)v, (int)t);
                case 3: return Color.FromArgb((int)p, (int)q, (int)v);
                case 4: return Color.FromArgb((int)t, (int)p, (int)v);
                default: return Color.FromArgb((int)v, (int)p, (int)q);
            }
        }

        private static Color EmbedBit(Color color, bool bit)
        {
            int r = bit ? (color.R | 1) : (color.R & ~1);
            return Color.FromArgb(color.A, r, color.G, color.B);
        }

        private static bool ExtractBit(Color color)
        {
            return (color.R & 1) == 1;
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
