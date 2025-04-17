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
        internal static (string keyS, Complex keyC) Generate(string text)
        {
            using (var sha256 = SHA256.Create())
            {
                string modifier = RandomNumberGenerator.GetHexString(16);
                byte[] combined = Encoding.UTF8.GetBytes($"{text}{modifier}{Environment.ProcessId}");
                byte[] hashBytes = sha256.ComputeHash(combined);
                var keyS = Convert.ToBase64String(hashBytes);
                if (!ValidateKey(keyS)) throw new ArgumentException("Invalid decryption key", nameof(keyS));
                var KeyC = GeneratedModifiedComplex(keyS);
                return (keyS, KeyC);
            }
        }

        private static Complex GeneratedModifiedComplex(string key)
        {
            byte[] keyBytes = Convert.FromBase64String(key);
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(keyBytes);
                double real = BitConverter.ToDouble(hashBytes, 0) % 1.0 - 0.5;
                double imag = BitConverter.ToDouble(hashBytes, 8) % 1.0 - 0.5;
                var KeyC = new Complex(real, imag);
                return KeyC;
            }
        }

        private static bool ValidateKey(string key)
        {
            try
            {
                byte[] keyBytes = Convert.FromBase64String(key);
                return keyBytes.Length == 32;
            }
            catch
            {
                return false;
            }
        }
    }
}
