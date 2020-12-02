using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using horizon.Client;
using horizon.Handshake;
using horizon.Packets;
using horizon.Transport;
using Microsoft.Extensions.Logging;
using wstreamlib;

namespace horizon.Server
{
    /// <summary>
    /// Horizon Server Instance
    /// </summary>
    public class HorizonServer
    {
        private HorizonServerConfig _config;
        private WStreamServer _wsServer;
        private X509Certificate2 _certificate;

        /// <summary>
        /// Configure the server
        /// </summary>
        /// <param name="config">Specify a Horizon Configuration</param>
        /// <param name="certificate">Pass in a SSL Certificate</param>
        public HorizonServer(HorizonServerConfig config, X509Certificate2 certificate = null)
        {
            _config = config;
            _wsServer = new WStreamServer();
            _certificate = certificate;
        }

        /// <summary>
        /// Start and bind the server to the port specified in the config. This will start listening to connections
        /// </summary>
        public void Start()
        {
            var ep = IPEndPoint.Parse(_config.Bind);
            Task.Run(()=> _wsServer.Listen(ep, _certificate));
            _wsServer.ConnectionAddedEvent += AcceptConnections;
            if (_certificate != null)
            {
                $"Server Started and is bound to wss://{_config.Bind}".Log(LogLevel.Information);
            }
            else
            {
                $"Server Started and is bound to ws://{_config.Bind}".Log(LogLevel.Information);
            }
        }
        /// <summary>
        /// Shut down the Server, and disconnect all the clients
        /// </summary>
        public void Stop()
        {
            $"Shutting Down Server...".Log(LogLevel.Information);
            _wsServer.Stop();
        }

        private void AcceptConnections(WsConnection connection)
        {
            // Log
            $"Client connection received with id {connection.ConnectionId}".Log(LogLevel.Debug);
            // Perform handshake
            var req = ServerHandshake.SecurityHandshake(new BinaryAdapter(connection), _config).Result;
            // Check if the client should be allowed
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
            // Create a new conduit
            var cd = new Conduit(connection);
            cd.OnDisconnect += ConduitOnDisconnect;
            // Check the connection type
            if (req.CType == ClientConnectRequest.ConnectType.Proxy)
            {
                // Open a proxy output pipe
                $"Proxy Client Connected. Proxying to {req.ProxyAddress}:{req.ProxyPort}".Log(LogLevel.Information);
                new HorizonOutput(new IPEndPoint(Dns.GetHostAddresses(req.ProxyAddress)[0], req.ProxyPort), cd);
            }
            else if(req.CType == ClientConnectRequest.ConnectType.ReverseProxy)
            {
                // Open a reverse proxy input pipe
                $"Reverse Proxy Client Connected. Bound on {req.ListenPort}".Log(LogLevel.Information);
                new HorizonInput(new IPEndPoint(IPAddress.Any, req.ListenPort), cd);
            }
            else
            {
                try
                {
                    connection.Close();
                }
                catch (Exception e)
                {
                    $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                }
            }
        }
        /// <summary>
        /// Handles client disconnection logging
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="connectionId"></param>
        /// <param name="remote"></param>
        private void ConduitOnDisconnect(DisconnectReason reason, Guid connectionId, bool remote)
        {
            if (remote)
            {
                $"The remote party has disconnected conduit id {connectionId}, with reason: {reason}".Log(LogLevel.Trace);
            }
            else
            {
                $"Disconnected from conduit id {connectionId}, with reason: {reason}".Log(LogLevel.Trace);
            }
        }
    }
}
