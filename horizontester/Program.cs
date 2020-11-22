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
using horizon.Protocol;
using horizon.Server;
using horizon.Transport;

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
                Bind = new IPEndPoint(IPAddress.Any, 2001)
            });
            server.Start();
            var client = new HorizonClient(new HorizonClientConfig()
            {
                ProxyConfig = new HorizonProxyConfig(){Port = 1234, Remote = "google.com"},
                Server = new Uri("ws://localhost:2001")
            });
            client.Start();
        }
    }
}
