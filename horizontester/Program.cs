using System;
using System.Collections.Generic;
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
            var server = new HorizonServer(new HorizonServerConfig()
            {
                AllowReverseProxy = true,
                AllowTunnel = true,
                Bind = new IPEndPoint(IPAddress.Any, 22001)
            });
            server.Start();
            var client = new HorizonClient(new HorizonClientConfig()
            {
                ProxyConfig = new HorizonProxyConfig(){Port = 80, Remote = "localhost"},
                Server = new Uri("ws://localhost:22001")
            });
            var x = client.Start().WaitAndUnwrapException();
            Thread.Sleep(-1);
        }
    }
}
