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
        private string tunnelDestination;
        private int tunnelDestinationPort;
        private Uri horizonHost;
        private bool stopFlag = false;
        private Socket localSock;

        public HorizonClient(Uri horizonHost, string destination, int port)
        {
            this.horizonHost = horizonHost;
            tunnelDestination = destination;
            tunnelDestinationPort = port;
        }

        /// <summary>
        /// Opens a tunnel on the local machine
        /// </summary>
        /// <param name="localBinding"></param>
        /// <param name="userId"></param>
        /// <param name="token"></param>
        /// <param name="ioConfig"></param>
        public void OpenTunnel(EndPoint localBinding, string userId, string token, HorizonOptions ioConfig)
        {
            ioManager = new IoManager(ioConfig);
            localSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            localSock.Bind(localBinding);
            localSock.Listen(1000);
            new Thread(() => SocketListener(userId, token)).Start();
        }

        /// <summary>
        /// Close the tunnel and disconnect
        /// </summary>
        public void CloseTunnel()
        {
            ioManager.Stop();
        }

        private void SocketListener(string userId, string token)
        {
            while (!stopFlag)
            {
                var sock = localSock.Accept();
                Connect(userId, token, sock);
            }
        }

        private void Connect(string userId, string token, Socket _localSock)
        {
            WStream client = new WStream();
            var connection = client.Connect(horizonHost, CancellationToken.None);
            if (Authenticate(userId, token, connection, out var req))
            {
                ioManager.AddIoConnection(connection, _localSock, req);
            }
            else
            {
                connection.Close();
                _localSock.Disconnect(false);
            }
        }

        private bool Authenticate(string userId, string token, WsConnection connection, out HorizonRequest req)
        {
            var request = new HorizonRequest
            {
                RequestedHost = tunnelDestination,
                RequestedPort = tunnelDestinationPort,
                UserId = userId.ToLower().Trim(),
                RequestTime = DateTime.UtcNow,
            };
            if (ProtocolManager.PerformClientHandshake(connection, token, request))
            {
                req = request;
                return true;
            }
            else
            {
                req = request;
                return false;
            }
        }
    }
}
