using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Windows.Forms;

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
                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new System.IO.MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
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
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new System.IO.MemoryStream())
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                {
                    cs.Write(encrypted, 0, encrypted.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        internal static (double real, double imag) GenerateFractalModifier(byte[] encrypted)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(encrypted);
                double real = BitConverter.ToDouble(hash, 0) % 2 - 1;
                double imag = BitConverter.ToDouble(hash, 8) % 2 - 1;
                return (real, imag);
            }
        }

        internal static string ComposeKeyString(byte[] key, byte[] iv, double real, double imag)
        {
            var parts = new[]
            {
                Convert.ToBase64String(key),
                Convert.ToBase64String(iv),
                real.ToString("F15"),
                imag.ToString("F15")
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
                double.Parse(parts[2]),
                double.Parse(parts[3])
            );
        }
    }
}
