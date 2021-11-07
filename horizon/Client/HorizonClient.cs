using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using horizon.Handshake;
using horizon.Packets;
using horizon.Transport;
using Microsoft.Extensions.Logging;
using wstream;

namespace horizon.Client
{
    /// <summary>
    /// Allows for proxying of TCP connections to and from the server
    /// </summary>
    public class HorizonClient
    {
        private HorizonClientConfig _config;
        private WsClient _signalClient;
        private WsClient _dataClient;
        private Conduit _conduit;
        private WsStream _conn = null;
        public event Conduit.DisconnectDelegate OnClientDisconnect;
        /// <summary>
        /// Configure the client
        /// </summary>
        /// <param name="config"></param>
        public HorizonClient(HorizonClientConfig config)
        {
            _config = config;
        }
        /// <summary>
        /// Start and Connect the client to the server
        /// </summary>
        /// <returns>Returns true if the client was successfully connected</returns>
        public async Task<bool> Start()
        {
            $"Connecting to {_config.Server}".Log(LogLevel.Information);
            // Start a WStream client
            _signalClient = new WsClient();
            // Connect the wstream client
            _conn = await _signalClient.ConnectAsync(_config.Server);
            // Check if the security handshake is successful
            var adpc = new BinaryAdapter(_conn);
            await adpc.WriteInt(0);
            var (success, key) = await ClientHandshake.SecurityHandshake(_conn, adpc, _config);
            if (success)
            {
                _conduit = new Conduit(_conn, key);
                _conduit.OnDisconnect += ConduitOnDisconnect;
                if (_config.ProxyConfig is HorizonReverseProxyConfig rproxcfg)
                {
                    $"Started Reverse Proxy!".Log(LogLevel.Information);
                    var v = new HorizonOutput(rproxcfg.LocalEndPoint, _conduit);
                    v.Initialize();
                }
                else if (_config.ProxyConfig is HorizonProxyConfig proxcfg)
                {
                    $"Started Proxy!".Log(LogLevel.Information);
                    var v = new HorizonInput(new IPEndPoint(IPAddress.Any, proxcfg.LocalPort), _conduit);
                    v.Initialize();
                }

                // create a data stream client
                _dataClient = new WsClient();
                var dataConn = await _dataClient.ConnectAsync(_config.Server);
                var adpd = new BinaryAdapter(dataConn);
                await adpd.WriteInt(1);
                await adpd.WriteByteArray(_conn.ConnectionId.ToByteArray());
                await _conduit.InitializeDataStreamAsync(dataConn);
                _conduit.ActivateConduit();
                return true;
            }
            $"Handshake Failed".Log(LogLevel.Debug);
            try
            {
                await _conn.CloseAsync();
            }
            catch(Exception e)
            {
                $"{e.Message} {e.StackTrace}".Log(LogLevel.Debug);
            }
            return false;
        }

        /// <summary>
        /// Called when the conduit is disconnected, re-fires the event for <see cref="OnClientDisconnect"/>
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
            try
            {
                _conn.Close();
            }
            catch (Exception e)
            {
                $"{e.Message} {e.StackTrace}".Log(LogLevel.Debug);
            }
            OnClientDisconnect?.Invoke(reason, connectionId, message, remote);
        }
        /// <summary>
        /// Call to Shutdown the client and disconnect (if applicable) from the server
        /// </summary>
        /// <returns></returns>
        public async Task Stop()
        {
            $"Shutting Down Client...".Log(LogLevel.Information);
            try
            {
                await _conduit.Disconnect();
            }
            catch(Exception e)
            {
                $"{e.Message} {e.StackTrace}".Log(LogLevel.Debug);
            }
            try
            {
                await _conn.CloseAsync();
            }
            catch (Exception e)
            {
                $"{e.Message} {e.StackTrace}".Log(LogLevel.Debug);
            }
        }
        
    }
}
