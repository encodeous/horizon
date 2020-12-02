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
        /// <summary>
        /// Create a input node, when called it will bind to the specified <see cref="IPEndPoint"/> and fibers will be created when there is a request to that socket
        /// </summary>
        /// <param name="localEp"></param>
        /// <param name="link"></param>
        /// <param name="minBuffer"></param>
        public HorizonInput(IPEndPoint localEp, Conduit link, int minBuffer = 1 << 18)
        {
            _minBufferSize = minBuffer;
            _hConduit = link;
            _hSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            LingerOption lo = new LingerOption(false, 1);
            _hSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, lo);
            _hSocket.Bind(localEp);
            // Create the listener on a separate thread
            Task.Run(Listen);
            _hConduit.OnDisconnect += HConduitOnOnDisconnect;
        }

        private void HConduitOnOnDisconnect(DisconnectReason reason, Guid connectionId, bool remote)
        {
            _hSocket.Close();
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
