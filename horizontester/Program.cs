using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using horizon;
using horizon.Protocol;
using horizon.Transport;

namespace horizontester
{
    class Program
    {
        static void Main(string[] args)
        {
            using var ms = new MemoryStream();
            HorizonTransformers transformerStack1 = new HorizonTransformers(ms, ms);
            var enc = new EncryptionTransformer();

            var enc2 = new EncryptionTransformer();

            enc2.CompleteEncryptionHandshake(enc.GetPublicKey());
            enc.CompleteEncryptionHandshake(enc2.GetPublicKey());

            transformerStack1.AddTransformer(enc);

            BinaryAdapter b1 = new BinaryAdapter(transformerStack1);

            for (int i = 1; i < 1000000; i++)
            {
                b1.WriteInt(i);
            }
            b1.Flush();
            using var ms2 = new MemoryStream(ms.ToArray());

            HorizonTransformers transformerStack2 = new HorizonTransformers(ms2, ms2);
            transformerStack2.AddTransformer(enc2);
            BinaryAdapter b2 = new BinaryAdapter(transformerStack2);

            for (int i = 1; i < 1000000; i++)
            {
                var x = b2.ReadInt();
                if (i != x)
                {
                    Console.WriteLine("Failed");
                }
            }
        }
    }
}
