using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using horizon.Transport;

namespace horizon.Transport
{
    /// <summary>
    /// A client that maps a Conduit connection to a Socket connection
    /// </summary>
    class HorizonOutput
    {
        // Server to connect to
        private string _hRemote;
        private int _hPort;
        public HorizonOutput(string remote, int port, Conduit link)
        {
            _hRemote = remote;
            _hPort = port;
            // Register the Fiber Creation delegate
            link.FiberCreate += FiberCreate;
        }

        private Fiber FiberCreate()
        {
            try
            {
                var sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(_hRemote, _hPort);
                return new Fiber(new NetworkStream(sock));
            }
            catch
            {
                // Do not allow the client to connect
                return null;
            }
        }
    }
}
