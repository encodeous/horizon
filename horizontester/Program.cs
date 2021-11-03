using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using horizon;
using horizon.Client;
using horizon.Server;
using Microsoft.Extensions.Logging;

Console.WriteLine("Server?");
char c = Console.ReadKey().KeyChar;
Logger.ApplicationLogLevel = LogLevel.Trace;
if (c == 'y')
{
    var server = new HorizonServer(new HorizonServerConfig()
    {
        Bind = "0.0.0.0:22001"
    });
    server.Start();
}
else
{
    var client = new HorizonClient(new HorizonClientConfig()
    {
        Token = "default",
        ProxyConfig = new HorizonProxyConfig()
        {
            RemoteEndpoint = "localhost",
            RemoteEndpointPort = 54900,
            LocalPort = 8080
        },
        Server = new Uri("ws://localhost:22001")
    });
    var x = await client.Start();
    Console.ReadLine();
    await client.Stop();
}
await Task.Delay(-1);