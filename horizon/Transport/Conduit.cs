using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using horizon.Packets;
using horizon.Threading;
using Microsoft.Extensions.Logging;
using wstream;
using wstream.Crypto;

namespace horizon.Transport
{
    /// <summary>
    /// A class that handles the routing and transport of "Fiber" connections
    /// </summary>
    public partial class Conduit
    {
        /// <summary>
        /// Checks if the Tunnel is still connected
        /// </summary>
        public bool Connected { get; private set; }
        private Channel<(int, ArraySegment<byte>)> _writeChannel = Channel.CreateBounded<(int, ArraySegment<byte>)>(500);
        private byte[] encryptionKey;
        internal readonly bool HighPerf = false;
        internal readonly BinaryAdapter Adapter;
        internal BinaryAdapter DataAdapter;
        internal readonly Dispatcher ActionDispatch;
        private DateTime _lastHeartBeat;
        private readonly ConcurrentDictionary<int, Fiber> _fibers;
        internal readonly WsStream _wsConn;
        internal WsStream _dataStream;

        public delegate void DisconnectDelegate(DisconnectReason reason, Guid clientId, string disconnectMessage, bool remote);
        /// <summary>
        /// Called on Disconnection of the Tunnel
        /// </summary>
        public event DisconnectDelegate OnDisconnect;

        public delegate Fiber CreateFiberCallback();
        /// <summary>
        /// Called on Creation of a new Fiber by the Remote
        /// </summary>
        public CreateFiberCallback FiberCreate;

        public Conduit(WsStream rawWs, byte[] key, bool perf)
        {
            HighPerf = perf;
            ActionDispatch = new Dispatcher();
            encryptionKey = key;
            _wsConn = rawWs;
            _fibers = new ConcurrentDictionary<int, Fiber>();
            // Wrap the binary stream for easier read/write
            Adapter = new BinaryAdapter(rawWs);
            Connected = true;
            // Register websocket disconnection
            rawWs.ConnectionClosedEvent += RawWsOnConnectionClosedEvent;
        }

        public async Task InitializeDataStreamAsync(WsStream stream)
        {
            if(!HighPerf) await stream.EncryptAesAsync(encryptionKey);
            DataAdapter = new BinaryAdapter(stream);
            Task.Factory.StartNew(DataWriter, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(DataReader, TaskCreationOptions.LongRunning);
        }

        public void ActivateConduit(int timeoutMilliseconds = 10000)
        {
            // Start Routing, Local Socket Checking and Heartbeat functions
            Task.Factory.StartNew(Router, TaskCreationOptions.LongRunning);
            ActionDispatch.Start();
            _lastHeartBeat = DateTime.Now;
            Task.Factory.StartNew(()=>Heartbeat(timeoutMilliseconds), TaskCreationOptions.LongRunning);
        }
        /// <summary>
        /// Heartbeat function, checks every <seealso cref="timeoutMilliseconds"/> milliseconds.
        /// </summary>
        /// <param name="timeoutMilliseconds"></param>
        /// <returns></returns>
        private async ValueTask Heartbeat(int timeoutMilliseconds)
        {
            while (Connected)
            {
                if (DateTime.Now - _lastHeartBeat > TimeSpan.FromMilliseconds(timeoutMilliseconds))
                {
                    $"The remote has not responded to the heartbeat! Disconnecting...".Log(LogLevel.Warning);
                    await Disconnect(DisconnectReason.Timeout);
                    break;
                }
                $"Sent heartbeat".Log(LogLevel.Trace);
                SendPacket(new SignalPacket(PacketType.Heartbeat));
                await Task.Delay(timeoutMilliseconds / 4);
            }
        }
        private async ValueTask InitializeFiber(int id)
        {
            var res = FiberCreate?.Invoke();
            // make sure a malicious server/client cannot create a fiber on an input node
            if (res != null)
            {
                res.Id = id;
                // Override existing fibers
                if (_fibers.ContainsKey(id)) await RemoveFiberInternal(id, true);
                _fibers[id] = res;
                res.StartAcceptingData();
                // Signal that the fiber is active
                SendPacket(new SignalPacket(PacketType.FiberAdded, id));
            }
            else
            {
                $"An unexpected fiber was rejected from connecting!".Log(LogLevel.Warning);
                // Invalid fiber packet, or rejected
                SendPacket(new SignalPacket(PacketType.FiberNotAdded, id));
            }
        }

        /// <summary>
        /// This function is called when the tunnel is abruptly closed
        /// </summary>
        /// <param name="connection"></param>
        private void RawWsOnConnectionClosedEvent(WsStream connection)
        {
            RemoteDisconnect(DisconnectReason.Terminated);
        }

        /// <summary>
        /// Call to signal that the Remote party has disconnected
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="message"></param>
        private void RemoteDisconnect(DisconnectReason reason = DisconnectReason.Shutdown, string message = null)
        {
            if (Connected)
            {
                Connected = false;
                OnDisconnect?.Invoke(reason, _wsConn.ConnectionId, message, true);
                if (_wsConn.Connected)
                {
                    try
                    {
                        _wsConn.Close();
                    }
                    catch(Exception e)
                    {
                        $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                    }
                    try
                    {
                        _dataStream.Close();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            ActionDispatch.Stop();
        }

        /// <summary>
        /// Disconnect from the remote, use <code>DisconnectReason.Terminated</code> to kill the connection
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="message"></param>
        public async Task Disconnect(DisconnectReason reason = DisconnectReason.Shutdown, string message = "")
        {
            if (Connected)
            {
                if (reason != DisconnectReason.Terminated)
                {
                    // Send graceful shutdown message
                    SendPacket(new DisconnectPacket()
                    {
                        Reason = reason,
                        StringReason = message
                    });
                    Task.Run(async () =>
                    {
                        // Wait for up to 5 seconds, or forcefully terminate the connection
                        int x = 0;
                        while (Connected && x < 50)
                        {
                            x++;
                            await Task.Delay(100);
                        }
                        // If the connection is still active, kill the connection
                        if (Connected)
                        {
                            try
                            {
                                await _wsConn.CloseAsync();
                            }
                            catch(Exception e)
                            {
                                $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                            }
                            Connected = false;
                            OnDisconnect?.Invoke(DisconnectReason.Terminated, _wsConn.ConnectionId, message, false);
                        }
                        else
                        {
                            OnDisconnect?.Invoke(reason, _wsConn.ConnectionId, message,false);
                        }
                    }).Start();
                }
                else
                {
                    Connected = false;
                    // Kill connection
                    if (_wsConn.Connected)
                    {
                        try
                        {
                            await _wsConn.CloseAsync();
                        }
                        catch(Exception e)
                        {
                            $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                        }
                    }
                    OnDisconnect?.Invoke(reason, _wsConn.ConnectionId, message,false);
                }
            }
            try
            {
                await _dataStream.CloseAsync();
            }
            catch
            {
                // ignored
            }
            ActionDispatch.Stop();
            Connected = false;
        }

        /// <summary>
        /// Disconnect and remove the fiber from the conduit (Queued)
        /// </summary>
        /// <param name="id"></param>
        /// <param name="remote"></param>
        public void RemoveFiber(int id, bool remote = false)
        {
            ActionDispatch.Add(() => RemoveFiberInternal(id, remote), Dispatcher.Priority.Slow);
        }
        private async ValueTask RemoveFiberInternal(int id, bool remote = false)
        {
            if (_fibers.ContainsKey(id))
            {
                if (_fibers[id].Connected)
                {
                    _fibers[id].Disconnect();
                    if (!remote)
                    {
                        var b = ArrayPool<byte>.Shared.Rent(4);
                        var a = BitConverter.GetBytes(id);
                        for (int i = 0; i < 4; i++)
                        {
                            b[i] = a[i];
                        }
                        await ForwardData(-1, new ArraySegment<byte>(b, 0, 4));
                        SendPacket(new SignalPacket(PacketType.FiberNotAccepting, id));
                    }
                }
                _fibers.Remove(id, out _);
            }
        }
        private readonly SemaphoreSlim _connectionSlim = new SemaphoreSlim(1);
        private int _idCnt = 0;

        /// <summary>
        /// Initiate a new fiber in the conduit
        /// </summary>
        /// <param name="fiber"></param>
        /// 
        public void CreateFiber(Fiber fiber)
        {
            ActionDispatch.Add(() => CreateFiberInternal(fiber), Dispatcher.Priority.Slow);
        }
        private async ValueTask CreateFiberInternal(Fiber fiber)
        {
            try
            {
                await _connectionSlim.WaitAsync();
                _idCnt++;
                fiber.Id = _idCnt;
                _fibers[_idCnt] = fiber;
                SendPacket(new SignalPacket(PacketType.AddFiber, _idCnt));
            }
            finally
            {
                _connectionSlim.Release();
            }
        }
    }
}
