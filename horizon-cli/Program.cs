using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using CommandLine;
using CommandLine.Text;
using horizon;
using horizon.Client;
using horizon.Server;
using Microsoft.Extensions.Logging;

namespace horizon_cli
{
    class Program
    {
        [Verb("server", HelpText = "Start Horizon as a server")]
        class ServerOptions
        {
            [Value(0, Default = -1, MetaName = "<server-port>", HelpText = "Horizon server bind port.", Required = false)]
            public int Port { get; set; }
            [Option('c', "config", Default = "", Required = false, HelpText = "Specify a horizon configuration path, defaults to current directory.")]
            public string ConfigPath { get; set; }
            [Option('s', "cert", Default = null, Required = false, HelpText = "Specify a SSL certificate path for securing connections.")]
            public string CertPath { get; set; }
            [Option('p', "cert-pass", Default = null, Required = false, HelpText = "SSL Certificate password (if applicable).")]
            public string CertPass { get; set; }
        }
        [Verb("client", HelpText = "Start Horizon as a client and connect to the specified Horizon server.")]
        class ClientOptions
        {
            [Value(0, MetaName = "<server-url>", HelpText = "Horizon server url.", Required = true)]
            public string ServerUrl { get; set; }

            [Value(1, MetaName = "<port-map>", HelpText = "Specifies a port mapping. <port>:<remote_server>:<port>:[R/P] Example: 22:ssh.example.com:22:P (proxies connections made to localhost:22 towards ssh.example.com:22), or 80::80:R (reverse proxies connections to the server on port 80 over to localhost:80)", Required = true)]
            public string PortMap { get; set; }

            [Value(2, MetaName = "<client-token>", Default = "default", HelpText = "Client authentication token. (Strongly recommended!)", Required = false)]
            public string Token { get; set; }
        }

        [Verb("about", HelpText = "About Horizon")]
        class About
        {
            
        }

        private static HorizonServer Server;
        private static HorizonClient Client;
        private static bool Stopped = false;

        static void Main(string[] args)
        {
#if DEBUG
            Logger.ApplicationLogLevel = LogLevel.Trace;
#endif
            Logger.ApplicationLogLevel = LogLevel.Information;
            Console.WriteLine(
                " |-------------------------------------------------------------------------------------| \n" +
                " |                        Horizon | High Performance TCP Proxy                         | \n" +
                " |                  [Start Horizon in either Client or Server mode]                    | \n" +
                " |             View the project at (https://github.com/encodeous/horizon)              | \n" +
                " |-------------------------------------------------------------------------------------| \n");

            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            Parser.Default.ParseArguments<ClientOptions, ServerOptions, About>(args).WithParsed((o) =>
            {
                if (o is About)
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
                        " |     ~~~    and dotnet/aspnetcore (https://github.com/dotnet/aspnetcore).       ~~~  | \n" +
                        " |~~                                                                           ~~     ~| \n" +
                        " |~ ~  ~     horizon-cli uses the wonderful Command Line Parser Library at  ~~   ~    ~| \n" +
                        " | ~      ~    ~  (https://github.com/commandlineparser/commandline)   ~~~~  ~~ ~  ~~  | \n" +
                        " |    ~ and is inspired by jpillora/chisel (https://github.com/jpillora/chisel)    ~   | \n" +
                        " |  ~    ~~~     ~~~                                              ~      ~~~     ~     | \n" +
                        " |    ~   ~                Created by Encodeous / Adam Chen.            ~~~   ~    ~   | \n" +
                        " | ~      ~~~  View the project at (https://github.com/encodeous/horizon)   ~~~    ~   | \n" +
                        " |-------------------------------------------------------------------------------------| \n");
                }
                else if (o is ServerOptions s)
                {
                    var cfg = new HorizonServerConfig();
                    if (File.Exists(Path.Combine(s.ConfigPath, "hconfig.json")))
                    {
                        cfg = Storage.GetServerConfig(Path.Combine(s.ConfigPath, "hconfig.json"));
                    }
                    else
                    {
                        Storage.SaveServerConfig(Path.Combine(s.ConfigPath, "hconfig.json"), cfg);
                    }

                    if (s.Port != -1)
                    {
                        cfg.Bind = "0.0.0.0:" + s.Port;
                    }

                    foreach (var user in cfg.Users)
                    {
                        if (user.Token == "default" || user.Token == "default-whitelist")
                        {
                            $"Default configuration detected, it is highly recommended to change the default authentication token!".Log(LogLevel.Critical);
                            break;
                        }
                    }

                    X509Certificate2 cert = null;
                    if (s.CertPath != null)
                    {
                        cert = s.CertPass != null ?
                            X509Certificate2.CreateFromEncryptedPemFile(s.CertPath, s.CertPass) :
                            X509Certificate2.CreateFromPemFile(s.CertPath);
                    }
                    Server = new HorizonServer(cfg, cert);
                    Server.Start();
                    $"Press Ctrl + C to exit".Log(LogLevel.Information);
                    while (!Stopped)
                    {
                        Thread.Sleep(100);
                    }
                }
                else if (o is ClientOptions c)
                {
                    $"Press Ctrl + C to exit".Log(LogLevel.Information);

                    var cfg = new HorizonClientConfig();

                    cfg.Token = c.Token;
                    Uri hUri = null;
                    if (Uri.TryCreate(c.ServerUrl, UriKind.Absolute, out var k))
                    {
                        if (k.Scheme != "wss" && k.Scheme != "ws")
                        {
                            if (Uri.TryCreate("ws://" + c.ServerUrl, UriKind.Absolute, out var j))
                            {
                                hUri = j;
                            }
                            else
                            {
                                $"The specified Server url was unable to be parsed! The url must have a scheme of either ws:// or wss://".Log(LogLevel.Critical);
                                return;
                            }

                        }
                        else
                        {
                            hUri = k;
                        }
                    }
                    else
                    {
                        $"The specified Server url was unable to be parsed! The url must have a scheme of either ws:// or wss://".Log(LogLevel.Critical);
                        return;
                    }
                    cfg.Server = hUri;
                    
                    Regex regex = new Regex("^(\\d+):(\\S+):(\\d+):([RPrp])$");
                    var match = regex.Match(c.PortMap);
                    if (match.Success)
                    {
                        if (match.Groups[4].Value.ToUpper() == "R")
                        {
                            var ccfg = new HorizonReverseProxyConfig();
                            ccfg.ListenPort = int.Parse(match.Groups[3].Value);
                            ccfg.LocalEndPoint = new IPEndPoint(IPAddress.Loopback, int.Parse(match.Groups[1].Value));
                            cfg.ProxyConfig = ccfg;
                        }
                        else
                        {
                            var ccfg = new HorizonProxyConfig();
                            ccfg.LocalPort = int.Parse(match.Groups[1].Value);
                            var serverString = match.Groups[2].Value;
                            ccfg.RemoteEndPoint = new DnsEndPoint(serverString, int.Parse(match.Groups[3].Value));
                        }
                    }
                    else
                    {
                        $"The specified portmap is not valid! A valid portmap follows this format: <local port>:<remote ip/domain, ignored for reverse proxy>:<remote port/bind port>:[R/P - reverse proxy or proxy]".Log(LogLevel.Critical);
                        return;
                    }

                    Client = new HorizonClient(cfg);
                    if (!Client.Start().Result)
                    {
                        $"The client failed to connect!".Log(LogLevel.Critical);
                        return;
                    }
                    while (!Stopped)
                    {
                        Thread.Sleep(100);
                    }
                }
            });
        }

        private static bool CheckEquals(HorizonServerConfig a, HorizonServerConfig b)
        {
            return JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
        }

        private static void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Client?.Stop().Wait();
            Server?.Stop();
            Stopped = true;
        }
    }
}
