using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using CommandLine;
using CommandLine.Text;
using horizon;
using wstreamlib;

namespace horizon_cli
{
    class Program
    {
        public class Options
        {
            [Option('a', "about", Required = false, HelpText = "About horizon")]
            public bool About { get; set; }

            [Option('s', "server", Required = false, HelpText = "[Server] Start horizon as a server")]
            public bool Server { get; set; }
            [Option('q', "authfile", Required = false, HelpText = "[Server] Specify a custom path to an auth file")]
            public string AuthFile { get; set; }
            [Option('p', "port", Required = false, HelpText = "[Server] Specify a port to listen to")]
            public int Port { get; set; }

            [Option('c', "client", Required = false, HelpText = "[Client] Start horizon as a client and connect to the specified horizon server --client <uri>")]
            public string Client { get; set; }
            [Option('m', "portmap", Required = false, HelpText = "[Client] Maps local ports to remote addresses. <port>:<remote_server>:<port> Example: 22:ssh.example.com:22")]
            public string PortMap { get; set; }
            [Option('u', "user", Required = false, HelpText = "[Client] Authentication username")]
            public string Username { get; set; }
            [Option('t', "token", Required = false, HelpText = "[Client] Authentication token (secret)")]
            public string Token { get; set; }

            [Option('b', "buffer", Required = false, HelpText = "[Client/Server] I/O buffer size")]
            public int BufferSize { get; set; }

            [Option('g', "config", Required = false, HelpText = "[Util] Generate an auth file through a wizard")]
            public bool AuthGen { get; set; }
        }
        static HorizonServer server;
        static HorizonClient client;
        static void Main(string[] args)
        {
            Console.WriteLine("horizon - high performance WebSocket tunnels");

            if (args.Length == 0) args = Console.ReadLine().Split(" ");

            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            Parser.Default.ParseArguments<Options>(args).WithParsed((o) =>
            {
                if (o.About)
                {
                    Console.WriteLine("             ^^                   @@@@@@@@@\r\n       ^^       ^^            @@@@@@@@@@@@@@@\r\n                            @@@@@@@@@@@@@@@@@@              ^^\r\n                           @@@@@@@@@@@@@@@@@@@@\r\n ~~~~ ~~ ~~~~~ ~~~~~~~~ ~~ &&&&&&&&&&&&&&&&&&&& ~~~~~~~ ~~~~~~~~~~~ ~~~\r\n ~         ~~   ~  ~       ~~~~~~~~~~~~~~~~~~~~ ~       ~~     ~~ ~\r\n   ~      ~~      ~~ ~~ ~~  ~~~~~~~~~~~~~ ~~~~  ~     ~~~    ~ ~~~  ~ ~~\r\n   ~  ~~     ~         ~      ~~~~~~  ~~ ~~~       ~~ ~ ~~  ~~ ~\r\n ~  ~       ~ ~      ~           ~~ ~~~~~~  ~      ~~  ~             ~~\r\n       ~             ~        ~      ~      ~~   ~             ~\r\n\r\n");
                    Console.WriteLine("Horizon is powered by encodeous/wstream (https://github.com/encodeous/wstream)\n" +
                                      "and ninjasource/Ninja.Websockets (https://github.com/ninjasource/Ninja.WebSockets)." +
                                      "\n" +
                                      "horizon-cli uses the wonderful Command Line Parser Library at\n" +
                                      "(https://github.com/commandlineparser/commandline)");
                    return;
                }
                if (o.AuthGen)
                {
                    Console.WriteLine("Horizon Authentication Generation Wizard");
                    new AuthGen().Generate();
                } else if (o.Server)
                {
                    Console.WriteLine("Running horizon in server mode.");

                    if (o.AuthFile == null)
                    {
                        if (!File.Exists("auth.json"))
                        {
                            Console.WriteLine("No auth.json found! Generating default auth file...");
                            PermissionHandler.SetPermissionInfo("auth.json", new List<UserPermission>(new[]{DefaultPermission()}));
                        }

                        o.AuthFile = "auth.json";
                    }
                    
                    var perms = PermissionHandler.GetPermissionInfo(o.AuthFile);
                    
                    server = new HorizonServer(perms);
                    var ep = new IPEndPoint(IPAddress.Any, o.Port);
                    if (o.BufferSize != 0)
                    {
                        server.Listen(ep, new HorizonOptions(){DefaultBufferSize = o.BufferSize});
                    }
                    else
                    {
                        server.Listen(ep, new HorizonOptions(){DefaultBufferSize = (int)HorizonOptions.OptimizedBuffer.ReducedLatency});
                    }
                    server.Start();
                    Console.WriteLine($"Horizon started on port: {o.Port}");
                    Thread.Sleep(-1);
                }
                else if (o.Client != null)
                {
                    if (o.PortMap == null)
                    {
                        Console.WriteLine("Horizon Client requires a port map.");
                        return;
                    }

                    if (o.Username == null || o.Token == null)
                    {
                        Console.WriteLine("Using default permissions");
                        o.Username = "default-user";
                        o.Token = "horizon";
                    }

                    string[] k = o.PortMap.Split(":");
                    var lep = new IPEndPoint(IPAddress.Any, int.Parse(k[0]));

                    client = new HorizonClient(new Uri(o.Client), k[1],int.Parse(k[2]));
                    if (o.BufferSize != 0)
                    {
                        client.OpenTunnel(lep, o.Username, o.Token, new HorizonOptions(){DefaultBufferSize = o.BufferSize});
                    }
                    else
                    {
                        client.OpenTunnel(lep, o.Username, o.Token, new HorizonOptions());
                    }
                    Console.WriteLine($"Horizon started | {o.Username}@ [{o.PortMap}] = > [{o.Client}].");
                    Thread.Sleep(-1);
                }
                else
                {
                    Console.WriteLine("Use the --help option to see help.");
                }
            });
        }

        private static UserPermission DefaultPermission()
        {
            return new UserPermission()
            {
                UserId = "default-user",
                UserToken = "horizon",
                AllowedRemoteServers = new []{"127.0.0.1","localhost","0.0.0.0"}.ToList(),
                AllowAnyPort = true
            };
        }

        private static void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (server != null)
            {
                Console.WriteLine("Stopping Horizon Server...");
                server.Close();
            }
            if (client != null)
            {
                Console.WriteLine("Stopping Horizon Client...");
                client.CloseTunnel();
            }
        }
    }
}
