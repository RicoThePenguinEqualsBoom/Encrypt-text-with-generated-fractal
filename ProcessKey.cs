using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SteganoTool
{
    internal class ProcessKey : IDisposable
    {
        private const int DefaultMaxIterations = 1000;

        internal Complex C { get; set; }
        internal required byte[] Salt { get; set; }
        internal int Iterations { get; set; }
        internal int TextLength { get; set; }

        /** internal static string Generate(int cryptL)
         {
             var initial = new Random(cryptL);

             int keyStarter = initial.Next(1, 50000000) + RandomNumberGenerator.GetInt32(20000000);

             var keyGenerator = new Random(keyStarter);

             double keyPart1 = keyGenerator.NextDouble();
             double keyPart2 = keyGenerator.NextDouble();

             string key = keyPart1 + "+" + keyPart2;

             return key;
         }**/

        public override string ToString()
        {
            return $"{C.Real}:{C.Imaginary}:{Convert.ToBase64String(Salt)}:{Iterations}:{TextLength}";
        }

        internal static ProcessKey FromString(string keyString)
        {
            var parts = keyString.Split(':');
            return new ProcessKey
            {
                C = new Complex(double.Parse(parts[0]), double.Parse(parts[1])),
                Salt = Convert.FromBase64String(parts[2]),
                Iterations = int.Parse(parts[3]),
                TextLength = int.Parse(parts[4])
            };
        }


        internal static ProcessKey Generate(string text)
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            using var sha256 = SHA256.Create();
            byte[] combinedBytes = new byte[salt.Length + textBytes.Length];
            Buffer.BlockCopy(salt, 0, combinedBytes, 0, salt.Length);
            Buffer.BlockCopy(textBytes, 0, combinedBytes, salt.Length, textBytes.Length);
            byte[] hash = sha256.ComputeHash(combinedBytes);

            double real = (BitConverter.ToDouble(hash, 0) % 2.0) - 1.0;
            double imaginary = (BitConverter.ToDouble(hash, 8) % 2.0) - 1.0;
            int iterations = DefaultMaxIterations + (BitConverter.ToInt32(hash, 16) % 500);
            int textLength = text.Length;

            Complex c = new(real, imaginary);
            var key = new ProcessKey { C = c, Salt = salt, Iterations = iterations, TextLength = textLength };

            MessageBox.Show(Decrypt(key));

            return key;
        }

        internal static string Decrypt(ProcessKey key)
        {
            using var sha256 = SHA256.Create();
            byte[] reconstructedHash = ReconstructHash(key.C, key.Iterations);

            byte[] combinedBytes = new byte[key.Salt.Length + key.TextLength];
            Buffer.BlockCopy(key.Salt, 0, combinedBytes, 0, key.Salt.Length);
            Buffer.BlockCopy(reconstructedHash, 0, combinedBytes, key.Salt.Length, key.TextLength);

            string decryptedText = Encoding.UTF8.GetString(combinedBytes, key.Salt.Length, key.TextLength);

            return decryptedText;
        }

        private static byte[] ReconstructHash(Complex c, int iterations)
        {
            byte[] hash = new byte[32];
            Buffer.BlockCopy(BitConverter.GetBytes(c.Real), 0, hash, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(c.Imaginary), 0, hash, 8, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(iterations), 0, hash, 16, 4);
            return hash;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
