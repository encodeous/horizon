using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using horizon.Client;
using horizon.Server;
using horizon.Transport;
using Microsoft.Extensions.Logging;

namespace horizon.Handshake
{
    /// <summary>
    /// An internal class to handle the entire connection process
    /// </summary>
    internal static class ClientHandshake
    {
        /// <summary>
        /// Determine if the client should connect to the server
        /// </summary>
        /// <param name="adp"></param>
        /// <param name="cfg"></param>
        /// <returns>returns false to disconnect</returns>
        internal static async ValueTask<bool> SecurityHandshake(BinaryAdapter adp, HorizonClientConfig cfg)
        {
            try
            {
                // Send a random byte sequence as the salt
                var sentBytes = Handshake.GetRandBytes(64);
                await adp.WriteByteArray(sentBytes);
                // Receive the salt from the server
                var remoteBytes = await adp.ReadByteArray();
                // Create a new client connection request
                var req = new ClientConnectRequest
                {
                    HashedBytes = Handshake.GetHCombined(remoteBytes, cfg.Token)
                };
                // Check if the client is configured as a proxy or reverse proxy
                if (cfg.ProxyConfig is HorizonProxyConfig pcfg)
                {
                    req.CType = ClientConnectRequest.ConnectType.Proxy;
                    req.ProxyAddress = pcfg.RemoteEndpoint;
                    req.ProxyPort = pcfg.RemoteEndpointPort;
                }
                else if (cfg.ProxyConfig is HorizonReverseProxyConfig rpcfg)
                {
                    req.CType = ClientConnectRequest.ConnectType.ReverseProxy;
                    req.ListenPort = rpcfg.ListenPort;
                }
                else
                {
                    $"Proxy Config Was Null!".Log(LogLevel.Critical);
                    throw new NullReferenceException();
                }
                // Serialize and send the request
                await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(req));
                // Get the server response
                var serverResponse = (ServerConnectionResponse)JsonSerializer.Deserialize(await adp.ReadByteArray(),
                    typeof(ServerConnectionResponse));

                Debug.Assert(serverResponse != null, nameof(serverResponse) + " != null");
                // Check if the connection was successful
                if (!serverResponse.Accepted)
                {
                    if (string.IsNullOrEmpty(serverResponse.DisconnectMessage))
                    {
                        $"Server refused connection!".Log(LogLevel.Warning);
                    }
                    else
                    {
                        $"Server refused connection with message: {serverResponse.DisconnectMessage}".Log(LogLevel.Warning);
                    }
                    return false;
                }
                // Check if the received token is valid (Ensures server authenticity)
                if (serverResponse.HashedBytes.SequenceEqual(Handshake.GetHCombined(sentBytes, cfg.Token)))
                {
                    // Signal Success
                    await adp.WriteInt(1);
                    return true;
                }
                // Signal Failure
                await adp.WriteInt(0);
                return false;
            }
            catch(Exception e)
            {
                $"Exception occurred on Client Handshake: {e.Message} {e.StackTrace}".Log(LogLevel.Debug);
            }

            return false;
        }
    }
}
