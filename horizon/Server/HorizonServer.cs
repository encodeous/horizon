using System;
using System.Collections.Concurrent;
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
        private ConcurrentDictionary<Guid, Conduit> _clients;

        /// <summary>
        /// Configure the server
        /// </summary>
        /// <param name="config">Specify a Horizon Configuration</param>
        public HorizonServer(HorizonServerConfig config)
        {
            _config = config;
            _wsServer = new WsServer();
            _clients = new ConcurrentDictionary<Guid, Conduit>();
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
            var adp = new BinaryAdapter(connection);
            int isData = await adp.ReadInt();
            if (isData == 1)
            {
                try
                {
                    var id = new Guid(await adp.ReadByteArray());
                    if (_clients.ContainsKey(id) && _clients[id]._dataStream == null)
                    {
                        $"Data stream connected with id {connection.ConnectionId}".Log(LogLevel.Debug);
                        await _clients[id].InitializeDataStreamAsync(connection);
                        _clients[id].ActivateConduit();
                        return;
                    }
                    else
                    {
                        
                    }
                }
                catch
                {
                    // ignored
                }
                try
                {
                    await connection.CloseAsync();
                }
                catch
                {
                    // ignored
                }
                return;
            }
            $"Client connection received with id {connection.ConnectionId}".Log(LogLevel.Debug);
            // Perform handshake
            var (req, key) = await ServerHandshake.SecurityHandshake(connection, adp, _config);
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
            var cd = new Conduit(connection, key, req.RequestHighPerf);
            cd.OnDisconnect += ConduitOnDisconnect;
            // Check the connection type
            if (req.CType == ClientConnectRequest.ConnectType.Proxy)
            {
                // Open a proxy output pipe
                $"Proxy Client Connected. Proxying to {req.ProxyAddress}:{req.ProxyPort}".Log(LogLevel.Information);
                var v = new HorizonOutput(new IPEndPoint((await Dns.GetHostAddressesAsync(req.ProxyAddress))[0], req.ProxyPort), cd);
                v.Initialize();
                _clients[connection.ConnectionId] = cd;
            }
            else if(req.CType == ClientConnectRequest.ConnectType.ReverseProxy)
            {
                // Open a reverse proxy input pipe
                $"Reverse Proxy Client Connected. Bound on {req.ListenPort}".Log(LogLevel.Information);
                var v = new HorizonInput(new IPEndPoint(IPAddress.Any, req.ListenPort), cd);
                v.Initialize();
                _clients[connection.ConnectionId] = cd;
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
            while (_clients.ContainsKey(connectionId)) _clients.TryRemove(connectionId, out _);
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
