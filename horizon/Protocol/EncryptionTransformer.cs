using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using horizon.Utilities;
using Sodium;

namespace horizon.Protocol
{
    /// <summary>
    /// An encryption layer powered by Libsodium and ECC
    /// </summary>
    public class EncryptionTransformer : IDataTransformer
    {
        public int TransformerOrder => 2;
        private KeyPair _encryptionKeypair;
        // private keys
        private byte[] _key = null;
        private byte[] _iv = null;
        private byte[] _hTokenBytes;
        /// <summary>
        /// A unique identifying Signature of a shared connection, consisting of 16 bytes
        /// </summary>
        public byte[] Signature = null;
        private SHA512 _hasher;
        private AesManaged _aesManaged;

        public EncryptionTransformer(string token)
        {
            _encryptionKeypair = PublicKeyBox.GenerateKeyPair();
            _hasher = SHA512.Create();
            // Hash the token to make sure there is no Man in the Middle attack (if a token is used)
            _hTokenBytes = _hasher.ComputeHash(Encoding.UTF8.GetBytes(token));
        }
        /// <summary>
        /// Returns the public key
        /// </summary>
        /// <returns></returns>
        public byte[] GetPublicKey()
        {
            return _encryptionKeypair.PublicKey;
        }
        /// <summary>
        /// Computes a secure shared key
        /// </summary>
        /// <param name="pubKey"></param>
        public void CompleteEncryptionHandshake(byte[] pubKey)
        {
            // compute a shared key
            var exc = ScalarMult.Mult(_encryptionKeypair.PrivateKey, pubKey);
            // hash the shared key
            var hashed = _hasher.ComputeHash(exc);
            // xor the hashed key with the token bytes
            for (int i = 0; i < hashed.Length; i++)
            {
                hashed[i] ^= _hTokenBytes[i];
            }
            // rehash the key to prevent potential token leaking
            hashed = _hasher.ComputeHash(hashed);
            // assign the first 32 bytes for the AES key
            _key = hashed.AsSpan(0, 32).ToArray();
            // assign the next 16 bytes as the initialization vector
            _iv = hashed.AsSpan(32, 16).ToArray();
            // assign the rest of the data as the Signature
            Signature = hashed.AsSpan(48, 16).ToArray();

            _aesManaged = new AesManaged();
        }
        /// <summary>
        /// Create the Encryption stream from current settings, called after handshake
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public Stream CreateTransformationStream(Stream original)
        {
            var transform = _aesManaged.CreateEncryptor(_key, _iv);
            return new CustomCryptoStream(new CryptoStream(original, transform,CryptoStreamMode.Write));
        }
        /// <summary>
        /// Create the Decryption stream from current settings, called after handshake
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public Stream CreateReverseTransformationStream(Stream original)
        {
            var transform = _aesManaged.CreateDecryptor(_key, _iv);
            return new CustomCryptoStream(new CryptoStream(original, transform, CryptoStreamMode.Read));
        }
    }
}
