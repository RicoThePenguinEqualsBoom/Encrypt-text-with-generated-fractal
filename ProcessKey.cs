using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace SteganoTool
{
    internal class ProcessKey
    {
        private const int KeySize = 32;
        private const int IvSize = 16;

        internal static (byte[] key,  byte[] iv) Generate()
        {
            byte[] key = new byte[KeySize];
            byte[] iv = new byte[IvSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
                rng.GetBytes(iv);
            }

            return (key, iv);
        }

        internal static byte[] EncryptWithAes(byte[] data, byte[] key,  byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using var encryptor = aes.CreateEncryptor();
                using var ms = new MemoryStream();
                using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        internal static byte[] DecryptWithAes(byte[] encrypted, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream();
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write);
                {
                    cs.Write(encrypted, 0, encrypted.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        internal static Complex GenerateFractalModifier(byte[] encrypted)
        {
            var hash = SHA256.HashData(encrypted);
            ulong realBits = BitConverter.ToUInt64(hash, 0);
            ulong imagBits = BitConverter.ToUInt64(hash, 8);

            double realNorm = realBits / (double)ulong.MaxValue;
            double imagNorm = imagBits / (double)ulong.MaxValue;

            double angle = realNorm * 2 * Math.PI;
            double radius = 0.75 + imagNorm * 0.25;
            double real = Math.Cos(angle) * radius;
            double imag = Math.Sin(angle) * radius;

            Complex c = new(real, imag);
            return c;
        }

        internal static string ComposeKeyString(byte[] key, byte[] iv, Complex c)
        {
            var parts = new[]
            {
                Convert.ToBase64String(key),
                Convert.ToBase64String(iv),
                c.Real.ToString("F15", CultureInfo.InvariantCulture),
                c.Imaginary.ToString("F15", CultureInfo.InvariantCulture)
            };
            return string.Join("|", parts);
        }

        internal static (byte[] key, byte[] iv, double real, double imag) ParseKeyString(string keyString)
        {
            var parts = keyString.Split('|');
            if (parts.Length != 4)
                throw new ArgumentException("bad key format");

            return (
                Convert.FromBase64String(parts[0]),
                Convert.FromBase64String(parts[1]),
                double.Parse(parts[2], CultureInfo.InvariantCulture),
                double.Parse(parts[3], CultureInfo.InvariantCulture)
            );
        }
    }
}
