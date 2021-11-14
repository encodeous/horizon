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
using wstream;
using wstream.Crypto;

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
        /// <param name="stream"></param>
        /// <param name="adp"></param>
        /// <param name="cfg"></param>
        /// <returns>returns false to disconnect</returns>
        internal static async ValueTask<(bool, byte[])> SecurityHandshake(WsStream stream, BinaryAdapter adp, HorizonClientConfig cfg)
        {
            try
            {
                // Send a random byte sequence as the salt
                var sentBytes = Handshake.GetRandBytes(64);
                await adp.WriteByteArray(sentBytes);
                
                // Receive the salt from the server
                var remoteBytes = await adp.ReadByteArray();
                
                // Send the hashed token to the server
                var cTokenHash = Handshake.GetHCombined(remoteBytes, cfg.Token);
                await adp.WriteByteArray(cTokenHash);
                if (await adp.ReadInt() != 1)
                {
                    $"The server has rejected the token provided!".Log(LogLevel.Critical);
                    return (false, null);
                }
                // Verify the server's token
                var serverHash = await adp.ReadByteArray();
                // Check if the received token is valid (Ensures server authenticity)
                if (!serverHash.SequenceEqual(Handshake.GetHCombined(sentBytes, cfg.Token)))
                {
                    $"The server responded with an unexpected response, a possible man-in-the middle attack may be occurring!".Log(LogLevel.Critical);
                    // Signal Failure
                    await adp.WriteInt(0);
                    return (false, null);
                }
                await adp.WriteInt(1);
                var encKey = Handshake.GetHCombined2(sentBytes, remoteBytes, cfg.Token);
                await stream.EncryptAesAsync(encKey);
                // SECURITY HEADER DONE

                var req = new ClientConnectRequest();
                req.RequestHighPerf = cfg.HighPerformance;
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
                    return (false, null);
                }
                return (true, encKey);
            }
            catch(Exception e)
            {
                $"Exception occurred on Client Handshake: {e.Message} {e.StackTrace}".Log(LogLevel.Debug);
            }

            return (false, null);
        }
    }
}
