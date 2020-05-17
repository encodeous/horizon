using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using wstreamlib;

namespace horizon
{
    public static class ProtocolManager
    {
        public static (bool, HorizonRequest) PerformServerHandshake(WsConnection clientStream,
            ConnectionValidator validator)
        {
            var clientRequest = ReadRequest(clientStream);
            var serverResponse = validator.HandleClientRequest(clientRequest);
            if (serverResponse == null)
            {
                return (false, null);
            }
            WriteResponse(clientStream, serverResponse);
            // Wait for client ok signal
            return (ReadStatus(clientStream), clientRequest);
        }

        public static bool PerformClientHandshake(WsConnection serverStream, string token, HorizonRequest connectionRequest)
        {
            ConnectionValidator.FillSecureToken(ref connectionRequest, token);
            WriteRequest(serverStream, connectionRequest);
            var serverResponse = ReadResponse(serverStream);
            if (serverResponse == null) return false;
            bool isValid = ConnectionValidator.VerifyServerResponse(token, connectionRequest, serverResponse);
            SendStatus(serverStream, isValid);
            return isValid;
        }

        public static bool ReadStatus(WsConnection clientStream)
        {
            byte[] bytes = new byte[4];
            bool flag = ReadExactly(clientStream, bytes);
            if (!flag) return false;
            int intValue = BitConverter.ToInt32(bytes);
            if (intValue != 0 && intValue != 1) return false;
            return intValue == 1;
        }

        public static void SendStatus(WsConnection serverStream, bool status)
        {
            serverStream.Write(BitConverter.GetBytes(status ? 1 : 0));
        }

        public static HorizonRequest ReadRequest(WsConnection clientStream)
        {
            var request = new HorizonRequest();
            // Read Time
            byte[] timeBytes = new byte[8];
            bool flag = ReadExactly(clientStream, timeBytes);
            if (!flag) return null;
            request.RequestTime = DateTime.FromBinary(BitConverter.ToInt64(timeBytes));

            // Read Requested Hosts
            byte[] lengthBytes = new byte[4];
            bool flag2_1 = ReadExactly(clientStream, lengthBytes);
            if (!flag2_1) return null;
            byte[] stringBytes = new byte[BitConverter.ToInt32(lengthBytes)];
            bool flag2_2 = ReadExactly(clientStream, stringBytes);
            if (!flag2_2) return null;
            request.RequestedHost = Encoding.UTF32.GetString(stringBytes);

            // Read Requested Ports
            byte[] portBytes = new byte[4];
            bool flag3_1 = ReadExactly(clientStream, portBytes);
            if (!flag3_1) return null;
            request.RequestedPort = BitConverter.ToInt32(portBytes);

            // Read User Id
            byte[] userIdBytesLength = new byte[4];
            bool flag5 = ReadExactly(clientStream, userIdBytesLength);
            if (!flag5) return null;
            byte[] userIdBytes = new byte[BitConverter.ToInt32(userIdBytesLength)];
            bool flag6 = ReadExactly(clientStream, userIdBytes);
            if (!flag6) return null;
            request.UserId = Encoding.UTF32.GetString(userIdBytes);

            // Read User Token Hash
            byte[] userTokenBytesLength = new byte[4];
            bool flag7 = ReadExactly(clientStream, userTokenBytesLength);
            if (!flag7) return null;
            byte[] userTokenHash = new byte[BitConverter.ToInt32(userTokenBytesLength)];
            bool flag8 = ReadExactly(clientStream, userTokenHash);
            if (!flag8) return null;
            request.UserTokenHash = userTokenHash;

            // Read User Salt
            byte[] userSaltBytesLength = new byte[4];
            bool flag9 = ReadExactly(clientStream, userSaltBytesLength);
            if (!flag9) return null;
            byte[] userSalt = new byte[BitConverter.ToInt32(userSaltBytesLength)];
            bool flag10 = ReadExactly(clientStream, userSalt);
            if (!flag10) return null;
            request.Salt = userSalt;
            return request;
        }

        public static void WriteRequest(WsConnection serverStream, HorizonRequest request)
        {
            // Write Time
            byte[] timeBytes = BitConverter.GetBytes(request.RequestTime.ToBinary());
            serverStream.Write(timeBytes);

            // Write Requested Hosts
            byte[] hostBytes = Encoding.UTF32.GetBytes(request.RequestedHost);
            serverStream.Write(BitConverter.GetBytes(hostBytes.Length));
            serverStream.Write(hostBytes);

            // Write Requested Ports
            serverStream.Write(BitConverter.GetBytes(request.RequestedPort));

            // Write User Id
            byte[] userIdBytes = Encoding.UTF32.GetBytes(request.UserId);
            serverStream.Write(BitConverter.GetBytes(userIdBytes.Length));
            serverStream.Write(userIdBytes);

            // Write User Token Hash
            serverStream.Write(BitConverter.GetBytes(request.UserTokenHash.Length));
            serverStream.Write(request.UserTokenHash);

            // Write Salt
            serverStream.Write(BitConverter.GetBytes(request.Salt.Length));
            serverStream.Write(request.Salt);
        }

        public static void WriteResponse(WsConnection clientStream, HorizonResponse response)
        {
            // Write Client Token
            var tokenBytes = response.ClientTokenHash;
            clientStream.Write(BitConverter.GetBytes(tokenBytes.Length));
            clientStream.Write(tokenBytes);

            // Write Randomly Generated Salt
            var saltBytes = response.Salt;
            clientStream.Write(BitConverter.GetBytes(saltBytes.Length));
            clientStream.Write(saltBytes);
        }

        public static HorizonResponse ReadResponse(WsConnection serverStream)
        {
            var response = new HorizonResponse();
            
            // Read Token Bytes
            byte[] tokenBytesLength = new byte[4];
            bool flag = ReadExactly(serverStream, tokenBytesLength);
            if (!flag) return null;
            byte[] tokenBytes = new byte[BitConverter.ToInt32(tokenBytesLength)];
            bool flag1_1 = ReadExactly(serverStream, tokenBytes);
            if (!flag1_1) return null;
            response.ClientTokenHash = tokenBytes;

            // Read Salt Bytes
            byte[] saltBytesLength = new byte[4];
            bool flag2 = ReadExactly(serverStream, saltBytesLength);
            if (!flag2) return null;
            byte[] saltBytes = new byte[BitConverter.ToInt32(saltBytesLength)];
            bool flag2_1 = ReadExactly(serverStream, saltBytes);
            if (!flag2_1) return null;
            response.Salt = saltBytes;

            return response;
        }

        public static bool ReadExactly(WsConnection stream, byte[] buffer)
        {
            int length = buffer.Length;
            if (length == 0)
            {
                return true;
            }
            var seg = new ArraySegment<byte>(buffer);
            int offset = 0;
            do
            {
                int bytesRead = stream.Read(seg.Slice(offset, length - offset));
                if (bytesRead == 0)
                {
                    return false;
                }

                offset += bytesRead;
            } while (offset < length);

            return true;
        }
    }
}
