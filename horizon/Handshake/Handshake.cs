using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace horizon.Handshake
{
    class Handshake
    {
        /// <summary>
        /// Computes a Hash based on a sequence of random bytes and the token, then hashed with SHA512
        /// </summary>
        /// <param name="input"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        internal static byte[] GetHCombined(byte[] input, string token)
        {
            byte[] hashed = SHA512.Create().ComputeHash(Encoding.Unicode.GetBytes(token));
            for (int i = 0; i < input.Length; i++)
            {
                hashed[i] ^= input[i];
            }

            return SHA512.Create().ComputeHash(hashed);
        }

        /// <summary>
        /// Computes a Hash based on a sequence of random bytes and the token, then hashed with SHA512
        /// </summary>
        /// <param name="input"></param>
        /// <param name="input2"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        internal static byte[] GetHCombined2(byte[] input, byte[] input2, string token)
        {
            byte[] hashed = SHA512.Create().ComputeHash(Encoding.Unicode.GetBytes(token));
            for (int i = 0; i < input.Length; i++)
            {
                hashed[i] ^= (byte)(input[i] ^ input2[i]);
            }

            return SHA512.Create().ComputeHash(hashed);
        }
        /// <summary>
        /// Get a sequence of cryptographically secure random bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        internal static byte[] GetRandBytes(int bytes)
        {
            byte[] bin = new byte[bytes];
            using var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(bin);
            return bin;
        }
    }
}
