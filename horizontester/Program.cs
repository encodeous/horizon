using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using horizon;

namespace horizontester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Horizon v1.0 by Encodeous powered by wstream");
            Console.WriteLine("[1] Host on localhost:1234 --(tunnel)> localhost:1235 via tcp.");
            Console.WriteLine("[2] Client on localhost:1233 --(wstream)> localhost:1234 via tcp.");
            string s = Console.ReadLine();
            if (s.StartsWith("1"))
            {
                Server();
            }
            else
            {
                Client();
            }
            Thread.Sleep(-1);
        }

        public static void Client()
        {
            HorizonClient hzc = new HorizonClient(new Uri("ws://localhost:1234"), "localhost", 1235, "encodeous", "encodeous123");
            hzc.OpenTunnel(new IPEndPoint(IPAddress.Any, 1233), new HorizonOptions());
        }
        public static void Server()
        {
            List<UserPermission> users = new List<UserPermission>
            {
                new UserPermission() {Administrator = true, UserId = "encodeous", UserToken = "encodeous123"}
            };
            HorizonServer wsts = new HorizonServer(users);
            wsts.Listen(new IPEndPoint(IPAddress.Any, 1234));
            wsts.Start();
        }
    }
}
