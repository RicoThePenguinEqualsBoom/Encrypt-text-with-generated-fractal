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
        internal string Key { get; set; }
        internal double RealC { get; set; }
        internal double ImaginaryC { get; set; }
        internal int Seed { get; set; }
        internal static readonly Complex C = new Complex(-0.7, 0.27015);

        internal static (ProcessKey, string) Generate(string text)
        {
            using var rng = RandomNumberGenerator.Create();
            var keyBytes = new byte[32];
            rng.GetBytes(keyBytes);
            var key = Convert.ToBase64String(keyBytes);

            var seed = new Random().Next();
            var random = new Random(seed);

            var modifiedC = new Complex(
                C.Real + (random.NextDouble() * 0.01 - 0.005),
                C.Imaginary + (random.NextDouble() * 0.01 - 0.005)
            );

            var fullKey = new ProcessKey
            {
                Key = key,
                RealC = modifiedC.Real,
                ImaginaryC = modifiedC.Imaginary,
                Seed = seed
            };

            var encryptedText = EncryptText(text, key);

            return (fullKey, encryptedText);
        }

        private static string EncryptText(string text, string key)
        {
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(key);
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(text);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            string encryptedText = Convert.ToBase64String(result);

            return encryptedText;
        }

        internal static string DecryptText(string encryptedText, string key)
        {
            var fullCipher = Convert.FromBase64String(encryptedText);

            using var aes = Aes.Create();
            var iv = new byte[aes.IV.Length];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.Key = Convert.FromBase64String(key);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            var decryptedText = Encoding.UTF8.GetString(plainBytes);

            return decryptedText;
        }

        internal static (bool[], bool[]) TextToBits(string text)
        {
            var bytes = Convert.FromBase64String(text);
            int messageLength = bytes.Length;

            bool[] lengthBits = new bool[32];
            for (int i = 0; i < 32; i++)
            {
                lengthBits[31 - i] = ((messageLength >> i) & 1) == 1;
            }

            var messageBits = new bool[messageLength * 8];
            for (int i = 0; i < messageLength; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    messageBits[i * 8 + j] = ((bytes[i] >> (7 - j)) & 1) == 1;
                }
            }

            return (lengthBits, messageBits);
        }
    }
}
