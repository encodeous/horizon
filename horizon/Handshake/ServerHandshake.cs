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
using wstream;
using wstream.Crypto;

namespace horizon.Handshake
{
    /// <summary>
    /// An internal class to handle the entire connection process
    /// </summary>
    internal static class ServerHandshake
    {
        /// <summary>
        /// Determines if a client is allowed to connect to the server
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="adp"></param>
        /// <param name="cfg"></param>
        /// <returns>returns what the client requested, or null if the client should not be allowed</returns>
        internal static async ValueTask<ClientConnectRequest> SecurityHandshake(WsStream stream, BinaryAdapter adp, HorizonServerConfig cfg)
        {
            try
            {
                // Send server salt bytes
                var sentBytes = Handshake.GetRandBytes(64);
                var cvTokenHash = Handshake.GetHCombined(sentBytes, cfg.Token);
                await adp.WriteByteArray(sentBytes);
                
                // Get the salt bytes from the client
                var remoteBytes = await adp.ReadByteArray();
                
                // Get the client's hashed token
                var cTokenHash = await adp.ReadByteArray();
                if (!cvTokenHash.SequenceEqual(cTokenHash))
                {
                    // Signal Failure
                    await adp.WriteInt(0);
                    return null;
                }
                await adp.WriteInt(1);

                var sTokenHash = Handshake.GetHCombined(remoteBytes, cfg.Token);
                // Make sure the client trusts the server
                await adp.WriteByteArray(sTokenHash);
                if (await adp.ReadInt() != 1)
                {
                    return null;
                }
                
                await stream.EncryptAesAsync(Handshake.GetHCombined2(sentBytes, remoteBytes, cfg.Token));
                // SECURITY HEADER DONE
                
                // Get the request from the client
                var clientRequest =
                    (ClientConnectRequest) JsonSerializer.Deserialize(await adp.ReadByteArray(),
                        typeof(ClientConnectRequest));
                
                var response = new ServerConnectionResponse();
                // Verify if the client is authorized to connect
                if (VerifyRequest(clientRequest, cfg))
                {
                    response.Accepted = true;
                }
                else
                {
                    response.Accepted = false;
                    response.DisconnectMessage =
                        "The specified connection requirements is not allowed by the server.";
                    await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                    return null;
                }
                // Validate the endpoint
                if ((string.IsNullOrEmpty(clientRequest.ProxyAddress) || clientRequest.ProxyPort < 0 || clientRequest.ProxyPort > 65535) && clientRequest.CType == ClientConnectRequest.ConnectType.Proxy)
                {
                    response.Accepted = false;
                    response.DisconnectMessage = "The specified endpoint is not valid";
                    await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                    return null;
                }
                // Check if the port is available if the client requested a reverse proxy
                if (clientRequest.CType == ClientConnectRequest.ConnectType.ReverseProxy &&
                    !CheckAvailableServerPort(clientRequest.ListenPort))
                {
                    response.Accepted = false;
                    response.DisconnectMessage =
                        "The specified port is already bound!";
                    await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                    return null;
                }
                // Send the server response
                await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                // Check if the client wants to connect
                return clientRequest;
            }
            catch(Exception e)
            {
                $"Exception occurred on Server Handshake: {e.Message} {e.StackTrace}".Log(LogLevel.Debug);
            }

            return null;
        }
        /// <summary>
        /// Checks if a port is already being used by another program, or connection
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Check if the client's request is allowed by the server configuration
        /// </summary>
        /// <param name="req"></param>
        /// <param name="conf"></param>
        /// <returns></returns>
        private static bool VerifyRequest(ClientConnectRequest req, HorizonServerConfig conf)
        {
            if (req.CType == ClientConnectRequest.ConnectType.Proxy)
            {
                bool matched = false;
                foreach (var pattern in conf.RemotesPattern)
                {
                    if (!Regex.IsMatch(req.ProxyAddress, pattern.HostRegex) || pattern.PortRangeStart > req.ProxyPort ||
                        req.ProxyPort > pattern.PortRangeEnd) continue;
                    matched = true;
                    break;
                }
                if (conf.Whitelist)
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
                bool matched = conf.ReverseBinds.Contains(req.ListenPort);
                if (conf.Whitelist)
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
    }
}
