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
    /// <summary>
    /// An internal class to handle the entire connection process
    /// </summary>
    internal static class ServerHandshake
    {
        /// <summary>
        /// Determines if a client is allowed to connect to the server
        /// </summary>
        /// <param name="adp"></param>
        /// <param name="cfg"></param>
        /// <returns>returns what the client requested, or null if the client should not be allowed</returns>
        internal static async ValueTask<ClientConnectRequest> SecurityHandshake(BinaryAdapter adp, HorizonServerConfig cfg)
        {
            try
            {
                // Send server salt bytes
                var sentBytes = Handshake.GetRandBytes(64);
                await adp.WriteByteArray(sentBytes);
                // Get the salt bytes from the client
                var remoteBytes = await adp.ReadByteArray();
                // Get the request from the client
                var clientRequest =
                    (ClientConnectRequest) JsonSerializer.Deserialize(await adp.ReadByteArray(),
                        typeof(ClientConnectRequest));

                Debug.Assert(clientRequest != null, nameof(clientRequest) + " != null");
                // Find the user token of the client by brute force
                var user = LookupUser(sentBytes, clientRequest.HashedBytes, cfg);
                var response = new ServerConnectionResponse();
                // Check if the user was successfully found
                if (user == null)
                {
                    response.Accepted = false;
                    response.DisconnectMessage = "The user was not found!";
                    await adp.WriteByteArray(JsonSerializer.SerializeToUtf8Bytes(response));
                    return null;
                }
                else
                {
                    // Verify if the client is authorized to connect
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
        /// <param name="user"></param>
        /// <returns></returns>
        private static bool VerifyRequest(ClientConnectRequest req, HorizonUser user)
        {
            if (req.CType == ClientConnectRequest.ConnectType.Proxy)
            {
                bool matched = false;
                foreach (var pattern in user.RemotesPattern)
                {
                    if (!Regex.IsMatch(req.ProxyAddress, pattern.HostRegex) || pattern.PortRangeStart > req.ProxyPort ||
                        req.ProxyPort > pattern.PortRangeEnd) continue;
                    matched = true;
                    break;
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
        /// <summary>
        /// Find the user by looking through all the users and checking the hash one by one, this prevents either side from stealing the client authentication token since it is actually not sent
        /// </summary>
        /// <param name="salt"></param>
        /// <param name="hash"></param>
        /// <param name="cfg"></param>
        /// <returns></returns>
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
