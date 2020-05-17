using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using wstreamlib;

namespace horizon.Transport
{
    public class IoManager
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

        internal void IoDisconnectCallback(WsConnection connection, HorizonRequest request)
        {
            $"{request.UserId} at {connection.RemoteEndPoint} has disconnected from {request.RequestedHost}:{request.RequestedPort}".Log();
        }

        public void AddIoConnection(WsConnection wstream, Socket sock, HorizonRequest request)
        {
            var ioWorker = new IoWorker(Options, wstream, sock, request, StopToken);
            ioWorker.StoppedCallback += IoDisconnectCallback;
            ioWorker.Start();
            $"{request.UserId} has connected from {wstream.RemoteEndPoint} to {request.RequestedHost}:{request.RequestedPort}".Log();
        }

        public void Stop()
        {
            _stopTokenSource.Cancel();
        }
    }
}
