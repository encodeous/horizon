using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
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

        private readonly byte[] _buffer;

        private readonly Conduit _hConduit;
        /// <summary>
        /// Create a fiber
        /// </summary>
        /// <param name="remote">The remote/local tcp connection opened</param>
        /// <param name="buffer">The read/write buffer</param>
        /// <param name="conduit">Where the data should be mirrored to</param>
        public Fiber(Socket remote, byte[] buffer, Conduit conduit)
        {
            _buffer = buffer;
            _bufferSize = buffer.Length;
            Connected = false;
            Remote = remote;
            Remote.NoDelay = true;
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
            Task.Run(Reader);
        }

        public async Task Reader()
        {
            try
            {
                while (Connected)
                {
                    var len = await Remote.ReceiveAsync(_buffer, SocketFlags.None);
                    if (len == 0)
                    {
                        $"REEEEEEEEEEEE".Log(LogLevel.Trace);
                        break;
                    }
                    await _hConduit.ForwardData(Id, new ArraySegment<byte>(_buffer, 0, len));
                }
            }
            catch(Exception e)
            {
                $"Exception occurred while reading from fiber {Id}: {e.Message} {e.StackTrace}".Log(LogLevel.Trace);
            }
            
            await _hConduit.RemoveFiber(Id);
            Remote.Dispose();
        }
        public bool Send(ArraySegment<byte> data)
        {
            try
            {
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
                _hConduit.RemoveFiber(Id).GetAwaiter().GetResult();
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
            _hConduit.Adapter._arrayPool.Return(_buffer);
            Connected = false;
        }
    }
}
