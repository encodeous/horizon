using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using horizon.Packets;
using horizon.Threading;
using Microsoft.Extensions.Logging;

namespace horizon.Transport
{
    /// <summary>
    /// A class that handles a single TCP connection
    /// </summary>
    public partial class Fiber
    {
        public const int MaxCachedPackets = 100;
        public const int BackpressureRelease = 50;
        public const int BackpressureStopLimit = 95;
        public bool Connected { get; internal set; }
        public Socket Remote;
        private NetworkStream _writeStream;
        internal Channel<ArraySegment<byte>> FiberBuffer;
        public int Id;
        readonly int _packetSize;
        private object _remoteBufferLock = new object();
        private int _remoteBufferRem;
        public bool IsReceivingData;
        public bool IsReadingData;
        private ManualResetEventSlim _releaseBackpressure = new ManualResetEventSlim(true);
        private byte[] _currentBuffer;
        private readonly Conduit _hConduit;
        private readonly bool _highPerf;
        public bool CrossedStopLimit;

        /// <summary>
        /// Create a fiber
        /// </summary>
        /// <param name="remote">The remote/local tcp connection opened</param>
        /// <param name="packetSize">The read/write buffer size</param>
        /// <param name="conduit">Where the data should be mirrored to</param>
        public Fiber(Socket remote, int packetSize, Conduit conduit)
        {
            _highPerf = conduit.HighPerf;
            _packetSize = packetSize;
            Connected = false;
            Remote = remote;
            _hConduit = conduit;
            _hConduit.OnDisconnect += HConduitOnOnDisconnect;
            // Max {MaxCachedPackets} packets can be buffered before backpressure is applied
            if(!_highPerf) FiberBuffer = Channel.CreateBounded<ArraySegment<byte>>(MaxCachedPackets);
        }

        /// <summary>
        /// Update backpressure
        /// </summary>
        public void BackpressureUpdate(int pressure)
        {
            if (_highPerf) return;
            if (pressure >= BackpressureStopLimit)
            {
                CrossedStopLimit = true;
            }
            else if(pressure <= BackpressureRelease && CrossedStopLimit && !IsReadingData)
            {
                IsReadingData = true;
                ResumeReceive();
            }
            lock (_remoteBufferLock)
            {
                _remoteBufferRem = pressure;
            }
        }
        
        /// <summary>
        /// Restarts receiving data from the tcp remote
        /// </summary>
        private void ResumeReceive()
        {
            _releaseBackpressure.Set();
        }
        public void StartAcceptingData()
        {
            $"Fiber {Id} in connection id {_hConduit._wsConn.ConnectionId} has started accepting data".Log(LogLevel.Trace);
            Connected = true;
            IsReadingData = true;
            IsReceivingData = true;
            _currentBuffer = ArrayPool<byte>.Shared.Rent(_packetSize);
            _writeStream = new NetworkStream(Remote);
            ResumeReceive();
            if(!_highPerf) Task.Factory.StartNew(Sender, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(Receiver, TaskCreationOptions.LongRunning);
        }

        
        private void HConduitOnOnDisconnect(DisconnectReason reason, Guid connectionId, string message, bool remote) => Disconnect();
        /// <summary>
        /// Disconnect the fiber
        /// </summary>
        public void Disconnect()
        {
            if (Connected)
            {
                try
                {
                    Connected = false;
                    $"Fiber {Id} in connection id {_hConduit._wsConn.ConnectionId} has disconnected from the remote".Log(LogLevel.Trace);
                    Remote.Close();
                    Remote.Dispose();
                    FiberBuffer.Writer.Complete();
                }
                catch(Exception e)
                {
                    $"{e.Message} {e.StackTrace}".Log(LogLevel.Debug);
                }
            }
        }
    }
}
