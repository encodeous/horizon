using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using horizon.Transport;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace horizon.Transport
{
    /// <summary>
    /// A client that maps a Conduit connection to a Socket connection
    /// </summary>
    class HorizonOutput
    {
        // Server to connect to
        private EndPoint _hEndpoint;
        private Conduit _hConduit;
        private int _minBufferSize;
        public HorizonOutput(EndPoint endPoint, Conduit link, int minBuffer = 1 << 18)
        {
            _hEndpoint = endPoint;
            _minBufferSize = minBuffer;
            _hConduit = link;
            // Register the Fiber Creation delegate
            link.FiberCreate += FiberCreate;
        }

        private Fiber FiberCreate()
        {
            try
            {
                var sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(_hEndpoint);
                return new Fiber(sock, _hConduit.Adapter._arrayPool.Rent(_minBufferSize), _hConduit);
            }
            catch(Exception e)
            {
                // Do not allow the client to connect
                $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                return null;
            }
        }
    }
}
