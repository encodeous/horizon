using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using horizon.Transport;

namespace horizon.Transport
{
    /// <summary>
    /// Allows Clients to connect to a Conduit
    /// </summary>
    class HorizonInput
    {
        // Output
        private Conduit _hConduit;
        // Input
        private Socket _hSocket;
        public HorizonInput(IPEndPoint localEp, Conduit link)
        {
            _hConduit = link;
            _hSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _hSocket.Bind(localEp);
            // Create the listener on a separate thread
            new Thread(Listen).Start();
        }
        /// <summary>
        /// Listener Function
        /// </summary>
        private void Listen()
        {
            _hSocket.Listen(100);
            while (_hConduit.Connected)
            {
                // Listen for clients and add a client fiber when a client connects
                var sock = _hSocket.Accept();
                var fiber = new Fiber(new NetworkStream(sock));
                _hConduit.AddFiber(fiber);
            }
            _hSocket.Dispose();
        }
    }
}
