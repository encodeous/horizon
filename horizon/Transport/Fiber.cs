using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using horizon.Packets;
using Microsoft.Extensions.Logging;

namespace horizon.Transport
{
    /// <summary>
    /// A class that handles a single TCP connection
    /// </summary>
    public class Fiber
    {
        /// <summary>
        /// Checks if the Fiber is still connected
        /// </summary>
        public bool Connected { get; internal set; }

        public Socket Remote;

        public int Id;

        private readonly int _bufferSize;

        public bool IsReceivingData;
        
        private byte[] _currentBuffer;

        private readonly Conduit _hConduit;
        /// <summary>
        /// Create a fiber
        /// </summary>
        /// <param name="remote">The remote/local tcp connection opened</param>
        /// <param name="bufferSize">The read/write buffer size</param>
        /// <param name="conduit">Where the data should be mirrored to</param>
        public Fiber(Socket remote, int bufferSize, Conduit conduit)
        {
            _bufferSize = bufferSize;
            Connected = false;
            Remote = remote;
            _hConduit = conduit;
            _hConduit.OnDisconnect += HConduitOnOnDisconnect;
        }

        private void HConduitOnOnDisconnect(DisconnectReason reason, Guid connectionId, string message, bool remote)
        {
            Disconnect();
        }
        /// <summary>
        /// Uses async sockets to ensure low and efficient cpu usage
        /// </summary>
        public void StartAcceptingData()
        {
            $"Fiber {Id} in connection id {_hConduit._wsConn.ConnectionId} has started accepting data".Log(LogLevel.Trace);
            Connected = true;
            IsReceivingData = true;
            _currentBuffer = _hConduit.Adapter._arrayPool.Rent(_bufferSize);
            Remote.BeginReceive(_currentBuffer, 0, _bufferSize, SocketFlags.None, ReadCallback, Remote);
        }

        public void ReadCallback(IAsyncResult ar)
        {
            try
            {
                var sock = (Socket) ar.AsyncState;
                if (sock == null || !sock.Connected)
                {
                    _hConduit.RemoveFiber(Id);
                    return;
                }
                int bytesRead = sock.EndReceive(ar);
                if (!sock.Connected || bytesRead == 0)
                {
                    _hConduit.RemoveFiber(Id);
                    return;
                }
                _hConduit.ForwardData(Id, new ArraySegment<byte>(_currentBuffer, 0, bytesRead)).GetAwaiter().GetResult();
                _currentBuffer = _hConduit.Adapter._arrayPool.Rent(_bufferSize);
                sock.BeginReceive(_currentBuffer, 0, _bufferSize, SocketFlags.None, ReadCallback, sock);
            }
            catch(Exception e)
            {
                $"Exception occurred while reading from fiber: {e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                _hConduit.RemoveFiber(Id);
            }
        }
        public bool Send(ArraySegment<byte> data)
        {
            try
            {
                if (!Remote.Connected) return false;
                int total = data.Count;
                int sent = 0;
                while (sent < total)
                {
                    int len = Remote.Send(data[sent..]);
                    sent += len;
                }
            }
            catch(Exception e)
            {
                $"Exception occurred while writing to fiber: {e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                _hConduit.RemoveFiber(Id);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Disconnect the fiber
        /// </summary>
        public void Disconnect()
        {
            if (Connected)
            {
                try
                {
                    $"Fiber {Id} in connection id {_hConduit._wsConn.ConnectionId} has disconnected from the remote".Log(LogLevel.Trace);
                    Remote.Close();
                    Remote.Dispose();
                }
                catch(Exception e)
                {
                    $"{e.Message} {e.StackTrace}".Log(LogLevel.Debug);
                }
            }
            Connected = false;
        }
    }
}
