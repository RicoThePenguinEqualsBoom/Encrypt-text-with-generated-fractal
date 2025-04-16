using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;

namespace SteganoTool
{
    internal class ProcessKey
    {
        internal static (string KeyS, Complex KeyC) Generate(string text)
        {
            using (var sha256 = SHA256.Create())
            {
                string modifier = RandomNumberGenerator.GetHexString(16);
                byte[] combined = Encoding.UTF8.GetBytes($"{text}{modifier}{Environment.ProcessId}");
                byte[] hashBytes = sha256.ComputeHash(combined);
                var KeyS = Convert.ToBase64String(hashBytes);
                var KeyC = GeneratedModifiedComplex(KeyS);
                return (KeyS, KeyC);
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
    }
}
