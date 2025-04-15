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
        private const int KeySize = 32;
        private const double PerturbationRange = 0.01;
        private const double PerturbationOffset = 0.005;

        internal string Key { get; private set; }
        internal double RealC { get; private set; }
        internal double ImaginaryC { get; private set; }
        internal int Seed { get; private set; }

        internal static readonly Complex BaseC = new Complex(-0.7, 0.27015);

        private ProcessKey() { }

        internal static (ProcessKey key, string encryptedText) Generate(string text)
        {
            Span<byte> keyBytes = stackalloc byte[KeySize];
            RandomNumberGenerator.Fill(keyBytes);
            var key = Convert.ToBase64String(keyBytes);

            var seed = BitConverter.ToInt32(RandomNumberGenerator.GetBytes(4));
            var random = new Random(seed);

            var modifiedC = GeneratedModifiedComplex(random);

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

        private static Complex GeneratedModifiedComplex(Random random)
        {
            return new Complex(
                BaseC.Real + (random.NextDouble() * PerturbationRange - PerturbationOffset),
                BaseC.Imaginary + (random.NextDouble() * PerturbationRange - PerturbationOffset)
            );
        }

        private static string EncryptText(string text, string key)
        {
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(key);
            aes.GenerateIV();

            var plainBytes = Encoding.UTF8.GetBytes(text);
            byte[] result;

            using (var encryptor = aes.CreateEncryptor())
            {
                var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                result = new byte[aes.IV.Length + cipherBytes.Length];

                aes.IV.CopyTo(result.AsSpan(0, aes.IV.Length));
                cipherBytes.CopyTo(result.AsSpan(aes.IV.Length));
            }

            string encryptedText = Convert.ToBase64String(result);

            return encryptedText;
        }

        internal static string DecryptText(string encryptedText, string key)
        {
            var fullCipher = Convert.FromBase64String(encryptedText);

            using var aes = Aes.Create();
            var ivLength = aes.IV.Length;

            ReadOnlySpan<byte> iv = fullCipher.AsSpan(0, ivLength);
            ReadOnlySpan<byte> cipher = fullCipher.AsSpan(ivLength);

            aes.Key = Convert.FromBase64String(key);
            aes.IV = iv.ToArray();

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher.ToArray(), 0, cipher.Length);

            var decryptedText = Encoding.UTF8.GetString(plainBytes);

            return decryptedText;
        }

        internal static (bool[] lengthBits, bool[] messageBits) TextToBits(string text)
        {
            var bytes = Convert.FromBase64String(text);
            int messageLength = bytes.Length;

            var lengthBits = new bool[32];
            var messageBits = new bool[messageLength * 8];

            for (int i = 0; i < 32; i++)
            {
                lengthBits[31 - i] = ((messageLength >> i) & 1) == 1;
            }

            const int chunkSize = 8;
            for (int i = 0; i < messageLength; i++)
            {
                int baseIndex = i * 8;
                byte currentByte = bytes[i];

                messageBits[baseIndex] = (currentByte & 0b10000000) != 0;
                messageBits[baseIndex + 1] = (currentByte & 0b01000000) != 0;
                messageBits[baseIndex + 2] = (currentByte & 0b00100000) != 0;
                messageBits[baseIndex + 3] = (currentByte & 0b00010000) != 0;
                messageBits[baseIndex + 4] = (currentByte & 0b00001000) != 0;
                messageBits[baseIndex + 5] = (currentByte & 0b00000100) != 0;
                messageBits[baseIndex + 6] = (currentByte & 0b00000010) != 0;
                messageBits[baseIndex + 7] = (currentByte & 0b00000001) != 0;
            }

            return (lengthBits, messageBits);
        }
    }
}
