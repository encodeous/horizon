using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CommandDotNet;
using horizon;
using horizon.Client;
using horizon.Server;
using horizon_cli.Models;

namespace horizon_cli
{
    [Command(Description = "A simple TCP tunneling utility")]
    public class HorizonCLI
    {
        [SubCommand]
        [Command(Description = "Connect to a Horizon server", Name = "connect")]
        public class Connect
        {
            [Command(Description = "Proxy connections through the server", Name = "proxy")]
            public async Task ProxyAsync(ProxyModel model)
            {
                var (p1, addr, p2) = Extensions.ParseMap(model.Portmap).Value;
                HorizonProxyConfig hpc = new HorizonProxyConfig()
                {
                    LocalPort = p1,
                    RemoteEndpoint = addr,
                    RemoteEndpointPort = p2
                };
                await StartClientAsync(model, hpc);
            }
            
            [Command(Description = "Reverse proxy connections from the server to this client", Name = "rproxy")]
            public async Task ReverseProxy(ReverseProxyModel model)
            {
                var (p1, addr, p2) = Extensions.ParseMap(model.Portmap).Value;
                var paddr = await Utils.ResolveDns(addr);
                if (paddr is null)
                {
                    Console.WriteLine($"The specified address \"{addr}\" used for reverse-proxying cannot be resolved with Dns. Make sure the address is correct and is reachable.");
                    return;
                }
                var hrpc = new HorizonReverseProxyConfig()
                {
                    ListenPort = p1,
                    LocalEndPoint = new IPEndPoint(paddr, p2)
                };
                await StartClientAsync(model, hrpc);
            }

            private async Task StartClientAsync(ClientModel model, IHorizonTunnelConfig cfg)
            {
                Logger.ApplicationLogLevel = model.LoggingLevel;
                HorizonClient hc = new HorizonClient(new HorizonClientConfig()
                {
                    HighPerformance = model.HighPerformance,
                    ProxyConfig = cfg,
                    Token = model.Token,
                    Server = new Uri((model.SecureWebsockets ? "wss://": "ws://") + model.ServerAddress)
                });
                TaskCompletionSource tcs = new TaskCompletionSource();
                Console.CancelKeyPress += (a, b) =>
                {
                    b.Cancel = true;
                    hc.Stop().GetAwaiter().GetResult();
                };
                hc.OnClientDisconnect += (s, e, v, q) =>
                {
                    tcs.SetResult();
                };
                if (await hc.Start())
                {
                    Console.WriteLine("Horizon has started, press Ctrl + C to gracefully shutdown.");
                    await tcs.Task;
                }
                else
                {
                    Console.WriteLine("Horizon has failed to start, please check the logs. (If not already) You can change the logging level to see more information.");
                }
            }
        }

        [Command(Description = "Host a Horizon server", Name = "host")]
        public async Task Host(ServerModel model)
        {
            Logger.ApplicationLogLevel = model.LoggingLevel;
            var cfg = new HorizonServerConfig()
            {
                Bind = model.Bind,
                Token = model.Token,
                RemotesPattern = model.RemotesFilter.Select(str =>
                {
                    if (!Extensions.ParseMap(str).HasValue) return null;
                    var (s1, s2, s3) = Extensions.ParseMap(str).Value;
                    return (object)new RemotePattern()
                    {
                        HostRegex = s2,
                        PortRangeStart = s1,
                        PortRangeEnd = s3
                    };
                }).Where(x => x is not null).Select(x=>(RemotePattern)x).ToArray(),
                Whitelist = model.Whitelist,
                ReverseBinds = model.ReverseBinds.Where(x => int.TryParse(x, out var k) && k is <= 65535 and > 0)
                    .Select(x => int.Parse(x)).ToArray()
            };
            HorizonServer hs = new HorizonServer(cfg);
            hs.Start();
            TaskCompletionSource tcs = new TaskCompletionSource();
            Console.CancelKeyPress += (a, b) =>
            {
                b.Cancel = true;
                hs.StopAsync().GetAwaiter().GetResult();
            };
            hs.OnServerStopped += () =>
            {
                tcs.SetResult();
            };
            Console.WriteLine("Horizon has started, press Ctrl + C to gracefully shutdown.");
            await tcs.Task;
        }
    }
}