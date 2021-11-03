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
using wstream;

namespace horizon.Server
{
    /// <summary>
    /// Horizon Server Instance
    /// </summary>
    public class HorizonServer
    {
        private HorizonServerConfig _config;
        private WsServer _wsServer;

        /// <summary>
        /// Configure the server
        /// </summary>
        /// <param name="config">Specify a Horizon Configuration</param>
        public HorizonServer(HorizonServerConfig config)
        {
            _config = config;
            _wsServer = new WsServer();
        }

        /// <summary>
        /// Start and bind the server to the port specified in the config. This will start listening to connections
        /// </summary>
        public void Start()
        {
            var ep = IPEndPoint.Parse(_config.Bind);
            Task.Run(async()=>
            {
                try
                {
                    await _wsServer.StartAsync(ep, AcceptConnectionsAsync);
                }
                catch (Exception e)
                {
                    await _wsServer.StopAsync();
                    $"{e.Message} {e.StackTrace}".Log(LogLevel.Error);
                }
            });
            $"Server Started and is bound to ws://{_config.Bind}".Log(LogLevel.Information);
        }
        /// <summary>
        /// Shut down the Server, and disconnect all the clients
        /// </summary>
        public void Stop()
        {
            $"Shutting Down Server...".Log(LogLevel.Information);
            _wsServer.StopAsync().GetAwaiter().GetResult();
        }

        private async Task AcceptConnectionsAsync(WsStream connection)
        {
            // Log
            $"Client connection received with id {connection.ConnectionId}".Log(LogLevel.Debug);
            // Perform handshake
            var req = await ServerHandshake.SecurityHandshake(connection, new BinaryAdapter(connection), _config);
            // Check if the client should be allowed
            if (req == null)
            {
                try
                {
                    await connection.CloseAsync();
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
                var v = new HorizonOutput(new IPEndPoint((await Dns.GetHostAddressesAsync(req.ProxyAddress))[0], req.ProxyPort), cd);
                v.Initialize();
                cd.ActivateConduit();
            }
            else if(req.CType == ClientConnectRequest.ConnectType.ReverseProxy)
            {
                // Open a reverse proxy input pipe
                $"Reverse Proxy Client Connected. Bound on {req.ListenPort}".Log(LogLevel.Information);
                var v = new HorizonInput(new IPEndPoint(IPAddress.Any, req.ListenPort), cd);
                v.Initialize();
                cd.ActivateConduit();
            }
            else
            {
                try
                {
                    await connection.CloseAsync();
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
        /// <param name="message"></param>
        /// <param name="remote"></param>
        private void ConduitOnDisconnect(DisconnectReason reason, Guid connectionId, string message, bool remote)
        {
            if (remote)
            {
                if (reason == DisconnectReason.Textual)
                {
                    $"The conduit id {connectionId} disconnected with message: {message}".Log(LogLevel.Warning);
                }
                else
                {
                    $"The remote party has disconnected conduit id {connectionId}, with reason: {reason}".Log(LogLevel.Trace);
                }
            }
            else
            {
                if (reason == DisconnectReason.Textual)
                {
                    $"Disconnected from conduit id {connectionId}: {message}".Log(LogLevel.Warning);
                }
                else
                {
                    $"Disconnected from conduit id {connectionId}, with reason: {reason}".Log(LogLevel.Trace);
                }
            }
        }
    }
}
