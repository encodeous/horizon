using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using CommandDotNet;
using horizon.Client;
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
            public async Task ProxyAsync(ProxyModel model, CancellationToken ct)
            {
                var (p1, addr, p2) = ParseProxyMap(model.Portmap);
                HorizonProxyConfig hpc = new HorizonProxyConfig()
                {
                    LocalPort = p1,
                    RemoteEndpoint = addr,
                    RemoteEndpointPort = p2
                };
                HorizonClient hc = new HorizonClient(new HorizonClientConfig()
                {
                    HighPerformance = model.HighPerformance,
                    ProxyConfig = hpc,
                    Token = model.Token
                });
                TaskCompletionSource tcs = new TaskCompletionSource();
                ct.Register(() =>
                {
                    hc.Stop().GetAwaiter().GetResult();
                });
                hc.OnClientDisconnect += (s, e, v, q) =>
                {
                    tcs.SetResult();
                };
                if (await hc.Start())
                {
                    await Task.WhenAny(Task.Delay(-1, ct), tcs.Task);
                }
            }
            
            [Command(Description = "Reverse proxy connections from the server to this client", Name = "rproxy")]
            public void ReverseProxy(ReverseProxyModel model, CancellationToken ct)
            {
            
            }
            
            private (int, string, int) ParseProxyMap(string portmap)
            {
                var portmapParts = portmap.Split(':');

                if (!int.TryParse(portmapParts[0], out var v) || v is <= 0 or > 65536)
                {
                    throw new Exception("Error occurred while parsing proxy map.");
                }

                if (!int.TryParse(portmapParts[2], out var x) || x is <= 0 or > 65536)
                {
                    throw new Exception("Error occurred while parsing proxy map.");
                }

                return (v, portmapParts[1], x);
            }
        }

        [Command(Description = "Host a Horizon server", Name = "host")]
        public void Host(ServerModel model, CancellationToken ct)
        {
            
        }
    }
}