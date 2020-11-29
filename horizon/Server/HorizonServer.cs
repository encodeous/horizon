using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using horizon.Client;
using horizon.Handshake;
using horizon.Transport;
using horizon.Utilities;
using Microsoft.Extensions.Logging;
using wstreamlib;

namespace horizon.Server
{
    public class HorizonServer
    {
        private HorizonServerConfig _config;
        private WStreamServer _wsServer;
        /// <summary>
        /// Configure the server
        /// </summary>
        /// <param name="config"></param>
        public HorizonServer(HorizonServerConfig config)
        {
            _config = config;
            _wsServer = new WStreamServer();
        }

        public void Start()
        {
            var ep = IPEndPoint.Parse(_config.Bind);
            Task.Run(()=> _wsServer.Listen(ep));
            _wsServer.ConnectionAddedEvent += AcceptConnections;
            $"Server Started and is bound to {_config.Bind}".Log(LogLevel.Information);
        }

        public void Stop()
        {
            $"Shutting Down Server...".Log(LogLevel.Information);
            _wsServer.Stop();
        }

        private void AcceptConnections(WsConnection connection)
        {
            $"Client connection received with id {connection.ConnectionId}".Log(LogLevel.Debug);
            var req = ServerHandshake.SecurityHandshake(new BinaryAdapter(connection), _config).Result;
            if (req == null)
            {
                try
                {
                    connection.Close();
                }
                catch(Exception e)
                {
                    $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                }
                return;
            }
            var cd = new Conduit(connection);
            if (req.CType == ClientConnectRequest.ConnectType.Proxy)
            {
                $"Proxy Client Connected. Proxying to {req.ProxyEndpoint}".Log(LogLevel.Information);
                new HorizonOutput(req.ProxyEndpoint, cd);
            }
            else if(req.CType == ClientConnectRequest.ConnectType.ReverseProxy)
            {
                $"Reverse Proxy Client Connected. Bound on {req.ListenPort}".Log(LogLevel.Information);
                new HorizonInput(new IPEndPoint(IPAddress.Any, req.ListenPort), cd);
            }
        }
    }
}
