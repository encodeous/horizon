using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using horizon;
using horizon.Client;
using horizon.Server;
using horizon.Transport;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

namespace horizontester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Server?");
            char c = Console.ReadKey().KeyChar;
            if (c == 'y')
            {
                var server = new HorizonServer(new HorizonServerConfig()
                {
                    Bind = new IPEndPoint(IPAddress.Any, 22001)
                });
                server.Start();
            }
            else
            {
                var client = new HorizonClient(new HorizonClientConfig()
                {
                    Token = "default",
                    ProxyConfig = new HorizonReverseProxyConfig()
                    {
                        ListenPort = 1234, 
                        LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 13824)
                    },
                    Server = new Uri("ws://192.168.1.8:22001")
                });
                var x = client.Start().WaitAndUnwrapException();
                Console.ReadLine();
                client.Stop().WaitAndUnwrapException();
            }
            Thread.Sleep(-1);
        }
    }
}
