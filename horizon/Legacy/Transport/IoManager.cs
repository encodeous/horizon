using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using wstreamlib;

namespace horizon.Legacy.Transport
{
    /// <summary>
    /// An internal class that manages and proxies data from point to point
    /// </summary>
    internal class IoManager
    {
        public HorizonOptions Options;
        public CancellationToken StopToken;
        private CancellationTokenSource _stopTokenSource;

        public IoManager(HorizonOptions opt)
        {
            Options = opt;
            _stopTokenSource = new CancellationTokenSource();
            StopToken = _stopTokenSource.Token;
        }

        /// <summary>
        /// This method is called when a connection is lost / disconnected
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="request"></param>
        internal void IoDisconnectCallback(WsConnection connection, HorizonRequest request)
        {
            $"{request.UserId} at {connection.RemoteEndPoint} has disconnected from {request.RequestedHost}:{request.RequestedPort}".Log(Logger.LoggingLevel.Info);
        }

        /// <summary>
        /// Register, and immediately start proxying data
        /// </summary>
        /// <param name="wstream"></param>
        /// <param name="sock"></param>
        /// <param name="request"></param>
        public void AddIoConnection(WsConnection wstream, Socket sock, HorizonRequest request)
        {
            var ioWorker = new IoWorker(Options, wstream, sock, request, StopToken);
            ioWorker.StoppedCallback += IoDisconnectCallback;
            ioWorker.Start();
            $"{request.UserId} has connected from {wstream.RemoteEndPoint} to {request.RequestedHost}:{request.RequestedPort}".Log(Logger.LoggingLevel.Info);
        }

        /// <summary>
        /// Stop the transfer of data, and disconnect
        /// </summary>
        public void Stop()
        {
            _stopTokenSource.Cancel();
        }
    }
}
