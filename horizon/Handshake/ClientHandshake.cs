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
    internal static class ClientHandshake
    {
        internal static async ValueTask<bool> SecurityHandshake(BinaryAdapter adp, HorizonClientConfig cfg)
        {
            try
            {
                var sentBytes = Handshake.GetRandBytes(64);
                await adp.WriteByteArray(sentBytes);

                var remoteBytes = await adp.ReadByteArray();

                var req = new ClientConnectRequest
                {
                    HashedBytes = Handshake.GetHCombined(remoteBytes, cfg.Token)
                };
                if (cfg.ProxyConfig is HorizonProxyConfig pcfg)
                {
                    req.CType = ClientConnectRequest.ConnectType.Proxy;
                    req.ProxyEndpoint = pcfg.RemoteEndPoint;
                }
                else if (cfg.ProxyConfig is HorizonReverseProxyConfig rpcfg)
                {
                    req.CType = ClientConnectRequest.ConnectType.ReverseProxy;
                    req.ListenPort = rpcfg.ListenPort;
                }
                
                await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(req));

                var serverResponse = (ServerConnectionResponse)JsonSerializer.Deserialize(await adp.ReadByteArray(),
                    typeof(ServerConnectionResponse));

                Debug.Assert(serverResponse != null, nameof(serverResponse) + " != null");
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

                if (serverResponse.HashedBytes.SequenceEqual(Handshake.GetHCombined(sentBytes, cfg.Token)))
                {
                    await adp.WriteInt(1);
                    return true;
                }

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
