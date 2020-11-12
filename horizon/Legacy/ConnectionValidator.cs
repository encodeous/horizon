using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Text;

namespace horizon.Legacy
{
    /// <summary>
    /// A class that handles authentication
    /// </summary>
    internal class ConnectionValidator
    {
        /// <summary>
        /// User ID to Permission Map
        /// </summary>
        public Dictionary<string, UserPermission> uidMap = new Dictionary<string, UserPermission>();
        /// <summary>
        /// Initialize Server Side Authentication
        /// </summary>
        /// <param name="perms"></param>
        public ConnectionValidator(List<UserPermission> perms)
        {
            foreach (var u in perms)
            {
                uidMap.Add(u.UserId.ToLower().Trim(), u);
            }
        }
        /// <summary>
        /// Authenticate Client Tokens
        /// </summary>
        /// <param name="clientRequest"></param>
        /// <returns></returns>
        public HorizonResponse HandleClientRequest(HorizonRequest clientRequest)
        {
            if (uidMap.ContainsKey(clientRequest.UserId.ToLower().Trim()))
            {
                // verify hash
                using SHA512 sha = new SHA512CryptoServiceProvider();
                using MemoryStream ms = new MemoryStream();
                ms.Write(Encoding.UTF32.GetBytes(uidMap[clientRequest.UserId.ToLower().Trim()].UserToken));
                ms.Write(clientRequest.Salt);
                ms.Write(BitConverter.GetBytes(clientRequest.RequestTime.ToBinary()));
                byte[] hash = sha.ComputeHash(ms);
                var val = VerifyClientPermissions(uidMap[clientRequest.UserId.ToLower().Trim()], clientRequest);

                if (!val) return null; // User does not have permissions to perform this connection

                if (FastCmp(hash, clientRequest.UserTokenHash))
                {
                    var ts = DateTime.UtcNow - clientRequest.RequestTime;

                    // Verify if token is expired
                    if (ts >= TimeSpan.FromSeconds(30))
                    {
                        $"{clientRequest.UserId} has failed to connect, the access token is expired (30 seconds).".Log(Logger.LoggingLevel.Info);
                        return null;
                    }
                    // Client successfully connected
                    return GenerateResponse(clientRequest.UserId.ToLower().Trim(), clientRequest);
                }

                $"{clientRequest.UserId} has failed to connect, the access token is invalid.".Log(Logger.LoggingLevel.Info);
                return null;
            }
            $"{clientRequest.UserId} has failed to connect, username not found".Log(Logger.LoggingLevel.Info);
            return null;
        }

        private bool VerifyClientPermissions(UserPermission user, HorizonRequest clientRequest)
        {
            if (user.Administrator) return true;
            if (!user.AllowAnyPort && (!user.AllowedRemotePorts.Contains(clientRequest.RequestedPort) ||
                                       user.DisallowedRemotePorts.Contains(clientRequest.RequestedPort)))
            {
                $"{clientRequest.UserId} has failed to connect, unauthorized port [{clientRequest.RequestedHost}:{clientRequest.RequestedPort}]".Log(Logger.LoggingLevel.Info);
                return false;
            }

            
            if (!user.AllowAnyServer &&
                (!user.AllowedRemoteServers.Contains(clientRequest.RequestedHost) ||
                 user.DisallowedRemoteServers.Contains(clientRequest.RequestedHost.ToLower().Trim())))
            {
                try
                {
                    string domain = DomainParse.GetDomain(clientRequest.RequestedHost);
                    if (!user.AllowAnyServer &&
                        (!user.AllowedRemoteServers.Contains(domain) ||
                         user.DisallowedRemoteServers.Contains(domain)))
                    {
                    
                        $"{clientRequest.UserId} has failed to connect, unauthorized host [{clientRequest.RequestedHost}:{clientRequest.RequestedPort}]".Log(Logger.LoggingLevel.Info);
                        return false;
                    }
                }
                catch
                {
                    $"{clientRequest.UserId} has failed to connect, unauthorized host [{clientRequest.RequestedHost}:{clientRequest.RequestedPort}]".Log(Logger.LoggingLevel.Info);
                    return false;
                }
            }

            return true;
        }

        private HorizonResponse GenerateResponse(string token, HorizonRequest request)
        {
            var response = new HorizonResponse();
            using RNGCryptoServiceProvider crng = new RNGCryptoServiceProvider();
            using SHA512 sha = new SHA512CryptoServiceProvider();
            using MemoryStream ms = new MemoryStream();
            ms.Write(Encoding.UTF32.GetBytes(token.ToLower().Trim()));
            byte[] salt = new byte[64];
            crng.GetBytes(salt);
            ms.Write(salt);
            ms.Write(BitConverter.GetBytes(request.RequestTime.ToBinary()));
            response.Salt = salt;
            response.ClientTokenHash = sha.ComputeHash(ms);

            return response;
        }

        public static void FillSecureToken(ref HorizonRequest request, string token)
        {
            using RNGCryptoServiceProvider crng = new RNGCryptoServiceProvider();
            using SHA512 sha = new SHA512CryptoServiceProvider();
            using MemoryStream ms = new MemoryStream();
            ms.Write(Encoding.UTF32.GetBytes(token.ToLower().Trim()));
            byte[] salt = new byte[64];
            crng.GetBytes(salt);
            ms.Write(salt);
            ms.Write(BitConverter.GetBytes(request.RequestTime.ToBinary()));
            request.Salt = salt;
            request.UserTokenHash = sha.ComputeHash(ms);
        }

        public static bool VerifyServerResponse(string token, HorizonRequest clientRequest,
            HorizonResponse serverResponse)
        {
            using SHA512 sha = new SHA512CryptoServiceProvider();
            using MemoryStream ms = new MemoryStream();
            ms.Write(Encoding.UTF32.GetBytes(token.ToLower().Trim()));
            if (FastCmp(clientRequest.Salt,serverResponse.Salt))
            {
                return false;
            }

            ms.Write(serverResponse.Salt);
            ms.Write(BitConverter.GetBytes(clientRequest.RequestTime.ToBinary()));
            byte[] hashedBytes = sha.ComputeHash(ms);
            return FastCmp(hashedBytes, serverResponse.ClientTokenHash);
        }

        /// <summary>
        /// Fast Binary Data Comparison
        /// </summary>
        /// <param name="ba"></param>
        /// <param name="bb"></param>
        /// <returns></returns>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal static unsafe bool FastCmp(byte[] ba, byte[] bb)
        {
            // Source: https://www.techmikael.com/2009/01/fast-byte-array-comparison-in-c.html
            int length = ba.Length;
            if (length != bb.Length)
            {
                return false;
            }
            fixed (byte* str = ba)
            {
                byte* chPtr = str;
                fixed (byte* str2 = bb)
                {
                    byte* chPtr2 = str2;
                    byte* chPtr3 = chPtr;
                    byte* chPtr4 = chPtr2;
                    while (length >= 10)
                    {
                        if ((((*(((int*)chPtr3)) != *(((int*)chPtr4))) || (*(((int*)(chPtr3 + 2))) != *(((int*)(chPtr4 + 2))))) || ((*(((int*)(chPtr3 + 4))) != *(((int*)(chPtr4 + 4)))) || (*(((int*)(chPtr3 + 6))) != *(((int*)(chPtr4 + 6)))))) || (*(((int*)(chPtr3 + 8))) != *(((int*)(chPtr4 + 8)))))
                        {
                            break;
                        }
                        chPtr3 += 10;
                        chPtr4 += 10;
                        length -= 10;
                    }
                    while (length > 0)
                    {
                        if (*(((int*)chPtr3)) != *(((int*)chPtr4)))
                        {
                            break;
                        }
                        chPtr3 += 2;
                        chPtr4 += 2;
                        length -= 2;
                    }
                    return (length <= 0);
                }
            }
        }
    }
}
