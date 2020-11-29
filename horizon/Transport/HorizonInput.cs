using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using horizon.Packets;
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
        private int _minBufferSize;
        public HorizonInput(IPEndPoint localEp, Conduit link, int minBuffer = 1 << 18)
        {
            _minBufferSize = minBuffer;
            _hConduit = link;
            _hSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _hSocket.Bind(localEp);
            // Create the listener on a separate thread
            Task.Run(Listen);
            _hConduit.OnDisconnect += HConduitOnOnDisconnect;
        }

        private void HConduitOnOnDisconnect(DisconnectReason reason)
        {
            _hSocket.Shutdown(SocketShutdown.Both);
            _hSocket.Dispose();
        }

        /// <summary>
        /// Listener Function
        /// </summary>
        private async Task Listen()
        {
            _hSocket.Listen(10);
            while (_hConduit.Connected)
            {
                // Listen for clients and add a client fiber when a client connects
                var sock = await _hSocket.AcceptAsync();
                var fiber = new Fiber(sock, _hConduit.Adapter._arrayPool.Rent(_minBufferSize), _hConduit);
                await _hConduit.AddFiber(fiber);
            }
            _hSocket.Dispose();
        }
    }
}
