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
        private const int MaxIterations = 1000000;
        private const double Zoom = 1.5;

        internal static Bitmap GenerateJulia(ProcessKey fullKey, int width, int height, string encryptedText)
        {
            var bits = ProcessKey.TextToBits(encryptedText);
            var bmp = new Bitmap(width, height);
            var bitIndex = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var point = new Complex(
                        (x - width / 2.0) / (width / 2.0) * Zoom,
                        (y - height / 2.0) / (height / 2.0) * Zoom
                    );

                    var modifiedC = new Complex(fullKey.RealC, fullKey.ImaginaryC);

                    var iterations = CalculateJuliaPoint(point, modifiedC);
                    var color = GetColor(iterations);

                    if (bitIndex < bits.Length)
                    {
                        color = EmbedBit(color, bits[bitIndex]);
                        bitIndex++;
                    }

                    bmp.SetPixel(x, y, color);
                }
            }

            return bmp;
        }

        private static int CalculateJuliaPoint(Complex z, Complex c)
        {
            int iterations = 0;
            while (iterations < MaxIterations && z.Magnitude < 2)
            {
                z = z * z + c;
                iterations++;
            }

            return iterations;
        }

        private static Color GetColor(int iterations)
        {
            if (iterations == MaxIterations)
                return Color.Black;

            var hue = (float)iterations / MaxIterations;
            return ColorFromHSV(hue * 360, 1, iterations < MaxIterations ? 1 : 0);
        }

        private static Color ColorFromHSV(double hue, double saturation, double value)
        {
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
            int r = color.R - (color.R % 2) + (bit ? 1 : 0);
            return Color.FromArgb(color.A, r, color.G, color.B);
        }

        private static bool ExtractBit(Color color)
        {
            return (color.R % 1) == 1;
        }

        private static int BitsToInt(bool[] bits)
        {
            int value = 0;
            for (int i = 0; i < 32; i++)
            {
                if (bits[i])
                {
                    value |= 1 << (31 - 1);
                }
            }

            return value;
        }

        private static string BitsToText(bool[] bits)
        {
            var bytes = new byte[bits.Length / 8];
            for (int i = 0; i < bits.Length; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (bits[i * 8 + j])
                    {
                        bytes[i] |= (byte)(1 << (7 - j));
                    }
                }
            }

            return Encoding.UTF8.GetString(bytes);
        }

        internal static string DecodeText(Bitmap bmp, ProcessKey fullKey)
        {
            var width = bmp.Width;
            var height = bmp.Height;
            var bits = new List<bool>();
            var messageLength = 0;

            for (int x = 0; x < width && messageLength < 32; x++)
            {
                for (int y = 0; y < height && messageLength < 32; y++)
                {
                    var color = bmp.GetPixel(x, y);
                    bits.Add(ExtractBit(color));
                    messageLength++;
                }    
            }

            var lengthBits = bits.Take(32).ToArray();
            var length = BitsToInt(lengthBits);
            bits.Clear();

            for (int x = 0; x < width; x++)
            { 
                for (int y = 0; y < height; y++)
                {
                    if (x * height + y < 32) continue;
                    if (bits.Count >= length * 8) break;

                    var color = bmp.GetPixel(x, y);
                    bits.Add(ExtractBit(color));
                }
            }

            var encryptedText = BitsToText(bits.ToArray());

            return encryptedText;
        }
    }
}
