using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using horizon.Transport;
using wstreamlib;

namespace horizon
{
    public class HorizonClient
    {
        public IoManager ioManager;
        private string _tunnelDestination;
        private int _tunnelDestinationPort;
        private Uri _horizonHost;
        private bool stopFlag = false;
        private Socket _localSock;
        private string _userId;
        private string _userToken;

        public HorizonClient(Uri horizonHost, string destination, int port, string userId, string userToken)
        {
            _userId = userId;
            _userToken = userToken;
            _horizonHost = horizonHost;
            _tunnelDestination = destination;
            _tunnelDestinationPort = port;
        }
        /// <summary>
        /// Pings the server to check credentials and latency
        /// </summary>
        /// <returns></returns>
        public PingResult Ping()
        {
            WStream client = new WStream();
            var connection = client.Connect(_horizonHost, CancellationToken.None);
            var s = AuthenticatePing(connection, out var latency);
            return new PingResult(){Latency = latency, Success = s};
        }

        /// <summary>
        /// Opens a tunnel on the local machine
        /// </summary>
        /// <param name="localBinding"></param>
        /// <param name="userId"></param>
        /// <param name="token"></param>
        /// <param name="ioConfig"></param>
        public void OpenTunnel(EndPoint localBinding, HorizonOptions ioConfig)
        {
            ioManager = new IoManager(ioConfig);
            _localSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _localSock.Bind(localBinding);
            _localSock.Listen(1000);
            new Thread(SocketListener).Start();
        }

        /// <summary>
        /// Close the tunnel and disconnect
        /// </summary>
        public void CloseTunnel()
        {
            ioManager.Stop();
        }

        private void SocketListener()
        {
            while (!stopFlag)
            {
                try
                {
                    var sock = _localSock.Accept();
                    Connect( sock);
                }
                catch
                {
                    "Failed to accept local client".Log(Logger.LoggingLevel.Severe);
                }
            }
        }

        private void Connect(Socket localSock)
        {
            try
            {
                WStream client = new WStream();
                var connection = client.Connect(_horizonHost, CancellationToken.None);
                if (Authenticate(connection, out var req))
                {
                    ioManager.AddIoConnection(connection, localSock, req);
                }
                else
                {
                    connection.Close();
                    localSock.Disconnect(false);
                }
            }
            catch
            {
                localSock.Disconnect(false);
                throw;
            }
        }

        private bool Authenticate(WsConnection connection, out HorizonRequest req)
        {
            var request = new HorizonRequest
            {
                RequestedHost = _tunnelDestination,
                RequestedPort = _tunnelDestinationPort,
                UserId = _userId.ToLower().Trim(),
                RequestTime = DateTime.UtcNow,
            };
            req = request;
            if (ProtocolManager.PerformClientHandshake(connection, _userToken, request))
            {
                return true;
            }
            return false;
        }

        private bool AuthenticatePing(WsConnection connection, out TimeSpan latency)
        {
            var request = new HorizonRequest
            {
                RequestedHost = _tunnelDestination,
                RequestedPort = _tunnelDestinationPort,
                UserId = _userId.ToLower().Trim(),
                RequestTime = DateTime.UtcNow,
                PingPacket = true
            };
            var response = ProtocolManager.PerformClientPing(connection, _userToken, request);

            latency = response - request.RequestTime;

            if (response != DateTime.MinValue)
            {
                return true;
            }

            return false;
        }
    }
}
