using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace horizon.Handshake
{
    class Handshake
    {
        internal static byte[] GetHCombined(byte[] input, string token)
        {
            byte[] hashed = SHA512.Create().ComputeHash(Encoding.Unicode.GetBytes(token));
            for (int i = 0; i < input.Length; i++)
            {
                hashed[i] ^= input[i];
            }

            return SHA512.Create().ComputeHash(hashed);
        }
        internal static byte[] GetRandBytes(int bytes)
        {
            byte[] bin = new byte[bytes];
            using var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(bin);
            return bin;
        }
    }
}
