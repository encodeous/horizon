using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using horizon.Client;
using horizon.Server;
using horizon.Transport;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace horizon.Handshake
{
    internal static class ServerHandshake
    {
        internal static async ValueTask<ClientConnectRequest> SecurityHandshake(BinaryAdapter adp, HorizonServerConfig cfg)
        {
            try
            {
                var sentBytes = Handshake.GetRandBytes(64);
                await adp.WriteByteArray(sentBytes);

                var remoteBytes = await adp.ReadByteArray();

                var clientRequest =
                    (ClientConnectRequest) JsonSerializer.Deserialize(await adp.ReadByteArray(),
                        typeof(ClientConnectRequest));

                Debug.Assert(clientRequest != null, nameof(clientRequest) + " != null");
                var user = LookupUser(sentBytes, clientRequest.HashedBytes, cfg);
                
                var response = new ServerConnectionResponse();

                if (user == null)
                {
                    response.Accepted = false;
                    response.DisconnectMessage = "The user was not found!";
                    await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                    return null;
                }
                else
                {
                    if (VerifyRequest(clientRequest, user))
                    {
                        response.Accepted = true;
                        response.HashedBytes = Handshake.GetHCombined(remoteBytes, user.Token);
                    }
                    else
                    {
                        response.Accepted = false;
                        response.DisconnectMessage =
                            "The specified connection requirements is not allowed by the server.";
                        await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                        return null;
                    }
                }
                if (clientRequest.ProxyEndpoint is not IPEndPoint 
                    && clientRequest.ProxyEndpoint is not DnsEndPoint && clientRequest.CType == ClientConnectRequest.ConnectType.Proxy)
                {
                    response.Accepted = false;
                    response.DisconnectMessage = "The specified endpoint type is not valid, valid types are IPEndPoint and DnsEndPoint.";
                    await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                    return null;
                }
                if (clientRequest.CType == ClientConnectRequest.ConnectType.ReverseProxy &&
                    !CheckAvailableServerPort(clientRequest.ListenPort))
                {
                    response.Accepted = false;
                    response.DisconnectMessage =
                        "The specified port is already bound!";
                    await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                    return null;
                }
                await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                if (await adp.ReadInt() == 1)
                {
                    return clientRequest;
                }
                return null;
            }
            catch(Exception e)
            {
                $"Exception occurred on Server Handshake: {e.Message} {e.StackTrace}".Log(LogLevel.Debug);
            }

            return null;
        }

        private static bool CheckAvailableServerPort(int port) {
            bool isAvailable = true;

            // https://stackoverflow.com/questions/570098/in-c-how-to-check-if-a-tcp-port-is-available
            // Evaluate current system tcp connections. This is the same information provided
            // by the netstat command line application, just in .Net strongly-typed object
            // form.  We will look through the list, and if our port we would like to use
            // in our TcpClient is occupied, we will set isAvailable to false.
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endpoint in tcpConnInfoArray) {
                if (endpoint.Port == port) {
                    isAvailable = false;
                    break;
                }
            }
            
            return isAvailable;
        }

        private static bool VerifyRequest(ClientConnectRequest req, HorizonUser user)
        {
            if (req.CType == ClientConnectRequest.ConnectType.Proxy)
            {
                bool matched = false;
                foreach (var pattern in user.RemotesPattern)
                {
                    if (req.ProxyEndpoint is DnsEndPoint a)
                    {
                        if (!Regex.IsMatch(a.Host, pattern.HostRegex) || pattern.PortRangeStart > a.Port ||
                            a.Port > pattern.PortRangeEnd) continue;
                        matched = true;
                        break;
                    }

                    if(req.ProxyEndpoint is IPEndPoint b)
                    {
                        if (!Regex.IsMatch(b.Address.ToString(), pattern.HostRegex) || pattern.PortRangeStart > b.Port ||
                            b.Port > pattern.PortRangeEnd) continue;
                        matched = true;
                        break;
                    }

                    return false;
                }
                if (user.Whitelist)
                {
                    return matched;
                }
                else
                {
                    return !matched;
                }
            }
            else if(req.CType == ClientConnectRequest.ConnectType.ReverseProxy)
            {
                bool matched = user.ReverseBinds.Contains(req.ListenPort);
                if (user.Whitelist)
                {
                    return matched;
                }
                else
                {
                    return !matched;
                }
            }

            return false;
        }

        private static HorizonUser LookupUser(byte[] salt, byte[] hash, HorizonServerConfig cfg)
        {
            foreach (var k in cfg.Users)
            {
                if (Handshake.GetHCombined(salt, k.Token).SequenceEqual(hash))
                {
                    return k;
                }
            }

            return null;
        }
    }
}
