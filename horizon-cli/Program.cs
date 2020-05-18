using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using CommandLine;
using CommandLine.Text;
using horizon;

namespace horizon_cli
{
    class Program
    {
        public class Options
        {
            [Option('a', "about", Required = false, HelpText = "About Horizon")]
            public bool About { get; set; }

            [Option('s', "server", Required = false, HelpText = "[Server] Start Horizon as a server")]
            public bool Server { get; set; }

            [Option('q', "authfile", Required = false, HelpText = "[Server] Specify a custom path to an auth file")]
            public string AuthFile { get; set; }

            [Option('p', "port", Required = false, HelpText = "[Server] Specify a port to listen to")]
            public int Port { get; set; } = -1;

            [Option('c', "client", Required = false,
                HelpText =
                    "[Client] Start Horizon as a client and connect to the specified Horizon server --client <uri>")]
            public string Client { get; set; }

            [Option('m', "portmap", Required = false,
                HelpText =
                    "[Client] Maps local ports to remote addresses. <port>:<remote_server>:<port> Example: 22:ssh.example.com:22")]
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
#if DEBUG
            Logger.Level = Logger.LoggingLevel.Debug;
#endif
            Console.WriteLine(
                " |-------------------------------------------------------------------------------------| \n" +
                " |                    Horizon | High Performance WebSocket Tunnels                     | \n" +
                " |                     [To view help, execute Horizon with --help]                     | \n" +
                " |             View the project at (https://github.com/encodeous/horizon)              | \n" +
                " |-------------------------------------------------------------------------------------| \n");

            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            Parser.Default.ParseArguments<Options>(args).WithParsed((o) =>
            {
                if (o.About)
                {
                    Console.WriteLine(
                        " |-------------------------------------------------------------------------------------| \n" +
                        " |      %%%             ^v^             ##########      %%%%%%%           %%%%%%%      | \n" +
                        " |   %%%%%          ^v^       ^v^     ##############        %%%%                       | \n" +
                        " |            %%%%%%%               ##################              ^v^                | \n" +
                        " |                                 ####################                %%%%%%          | \n" +
                        " |________________________________ &_&&&&&_&&&__&&_&_&&________________________________| \n" +
                        " |  _    _         __   ~  ~       ____________________ ~       __     __ ~     __   ~ | \n" +
                        " |~   ~    ~      ~~      __ __ __  _____________ ____  ~     ___    _ ___  ~ ~~   _   | \n" +
                        " |   ___   ~  ~~     ~         ~      ______  ______       __ ~ __  ~~ ~         ~____ | \n" +
                        " |   ~   ~  ~ ____~~~ ~      ~  ~~       __ ~____~  ~      ~_  ~      ______ ~~        | \n" +
                        " | ~~~~~       ~   ~    ~~   ~        ~      ~      ~~   ~ ~~~    ~    ~      ~   ~ ~  | \n" +
                        " | ~~                                                                               ~~ | \n" +
                        " |~~  Horizon is powered by encodeous/wstream (https://github.com/encodeous/wstream)  ~| \n" +
                        " | and ninjasource/Ninja.Websockets (https://github.com/ninjasource/Ninja.WebSockets). | \n" +
                        " |~~                                                                           ~~     ~| \n" +
                        " |~ ~  ~     horizon-cli uses the wonderful Command Line Parser Library at  ~~   ~    ~| \n" +
                        " | ~      ~    ~  (https://github.com/commandlineparser/commandline)   ~~~~  ~~ ~  ~~  | \n" +
                        " |    ~     ~    ~    and uses Newtonsoft.Json for configuration.    ~      ~~     ~~  | \n" +
                        " |  ~    ~~~     ~~~                                              ~      ~~~     ~     | \n" +
                        " |    ~   ~           Created with care by Encodeous / Adam Chen.       ~~~   ~    ~   | \n" +
                        " | ~      ~~~  View the project at (https://github.com/encodeous/horizon)   ~~~    ~   | \n" +
                        " |-------------------------------------------------------------------------------------| \n");
                    return;
                }

                if (o.AuthGen)
                {
                    Console.WriteLine("Horizon Authentication Generation Wizard");
                    new AuthGen().Generate();
                }
                else if (o.Server)
                {
                    Console.WriteLine("Running Horizon in server mode.");

                    if (o.AuthFile == null)
                    {
                        if (!File.Exists("auth.json"))
                        {
                            Console.WriteLine("No auth.json found! Generating default auth file...");
                            PermissionHandler.SetPermissionInfo("auth.json",
                                new List<UserPermission>(new[] {DefaultPermission()}));
                        }

                        o.AuthFile = "auth.json";
                    }

                    if (o.Port == -1)
                    {
                        Console.WriteLine("Please specify a port with -p or --port.");
                        return;
                    }

                    var perms = PermissionHandler.GetPermissionInfo(o.AuthFile);

                    server = new HorizonServer(perms);

                    var ep = new IPEndPoint(IPAddress.Any, o.Port);
                    if (o.BufferSize != 0)
                    {
                        server.Listen(ep, new HorizonOptions() {DefaultBufferSize = o.BufferSize});
                    }
                    else
                    {
                        server.Listen(ep,
                            new HorizonOptions()
                                {DefaultBufferSize = (int) HorizonOptions.OptimizedBuffer.ReducedLatency});
                    }

                    server.Start();
                    Console.WriteLine($"Horizon started on port: {o.Port}");
                    Thread.Sleep(-1);
                }
                else if (o.Client != null)
                {
                    var s = Extensions.GetScheme(o.Client);
                    if (s != "http" && s != "https" && s != "ws" && s != "wss")
                    {
                        Console.WriteLine($"Unrecognized Url Scheme \"{s}\". Valid schemes are (http/s, ws/s)");
                        return;
                    }

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

                    var k = o.PortMap.Split(":");

                    var localEp = new IPEndPoint(IPAddress.Any, int.Parse(k[0]));

                    client = new HorizonClient(new Uri(o.Client), k[1], int.Parse(k[2]), o.Username, o.Token);

                    var ping = client.Ping();

                    if (ping.Success)
                    {
                        Console.WriteLine($"Pinged server, Latency: ({ping.Latency.TotalMilliseconds} ms).");
                    }
                    else
                    {
                        Console.WriteLine($"Could not ping server, double check the username/token or the server uri.");
                        return;
                    }

                    client.OpenTunnel(localEp,
                        o.BufferSize != 0
                            ? new HorizonOptions {DefaultBufferSize = o.BufferSize}
                            : new HorizonOptions());
                    Console.WriteLine($"Horizon started | {o.Username}@ [{o.PortMap}] = > [{o.Client}].");
                    Thread.Sleep(-1);
                }
                else
                {
                    Console.WriteLine(
                        " -                          [Press Any Key to exit...]                                 - ");
                    Console.ReadKey();
                }
            });
        }

        private static UserPermission DefaultPermission()
        {
            return new UserPermission()
            {
                UserId = "default-user",
                UserToken = "horizon",
                AllowedRemoteServers = new[] {"127.0.0.1", "localhost", "0.0.0.0"}.ToList(),
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
