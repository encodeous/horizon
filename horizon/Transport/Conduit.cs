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
using Microsoft.Extensions.Logging;
using wstream;
using wstream.Crypto;

namespace horizon.Transport
{
    /// <summary>
    /// A class that handles the routing and transport of "Fiber" connections
    /// </summary>
    public class Conduit
    {
        private ConcurrentQueue<int> _disconnectQueue = new ConcurrentQueue<int>();
        private Channel<int> _connectQueue = Channel.CreateUnbounded<int>();
        private Channel<(int, ArraySegment<byte>)> _writeChannel = Channel.CreateBounded<(int, ArraySegment<byte>)>(100);
        private byte[] encryptionKey;

        internal readonly BinaryAdapter Adapter;
        internal BinaryAdapter DataAdapter;
        /// <summary>
        /// Checks if the Tunnel is still connected
        /// </summary>
        public bool Connected { get; private set; }
        /// <summary>
        /// Last heartbeat packet received
        /// </summary>
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

        public Conduit(WsStream rawWs, byte[] key)
        {
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
            await stream.EncryptAesAsync(encryptionKey);
            DataAdapter = new BinaryAdapter(stream);
            Task.Run(DataWriter);
            Task.Run(DataReader);
        }

        public void ActivateConduit(int timeoutMilliseconds = 10000)
        {
            // Start Routing, Local Socket Checking and Heartbeat functions
            Task.Run(Router);
            Task.Run(FiberCreator);
            Task.Run(Dispatcher);
            _lastHeartBeat = DateTime.Now;
            Task.Run(()=>Heartbeat(timeoutMilliseconds));
        }
        /// <summary>
        /// Heartbeat function, checks every <seealso cref="timeoutMilliseconds"/> milliseconds.
        /// </summary>
        /// <param name="timeoutMilliseconds"></param>
        /// <returns></returns>
        private async Task Heartbeat(int timeoutMilliseconds)
        {
            while (Connected)
            {
                if (DateTime.Now - _lastHeartBeat > TimeSpan.FromMilliseconds(timeoutMilliseconds))
                {
                    $"The remote has not responded to the heartbeat! Disconnecting...".Log(LogLevel.Warning);
                    await Disconnect(DisconnectReason.Timeout);
                    break;
                }
                SendPacket(new SignalPacket(PacketType.Heartbeat));
                await Task.Delay(timeoutMilliseconds / 4);
            }
        }
        /// <summary>
        /// Starts and initializes sockets on a separate thread since this can take some time and we don't want to delay the dispatch queue.
        /// </summary>
        /// <returns></returns>
        private async Task FiberCreator()
        {
            while (Connected)
            {
                var id = await _connectQueue.Reader.ReadAsync();
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
            Connected = false;
        }
        public readonly Channel<Func<Task>> DispatchQueue = Channel.CreateUnbounded<Func<Task>>();
        
        /// <summary>
        /// Dispatches packets/operations on a single thread to prevent a race event
        /// </summary>
        /// <returns></returns>
        private async Task Dispatcher()
        {
            while (Connected)
            {
                var val = await DispatchQueue.Reader.WaitToReadAsync();
                if (!val) break;
                
                try
                {
                    await Adapter._writeSlim.WaitAsync();
                    await (await DispatchQueue.Reader.ReadAsync()).Invoke();
                }
                finally
                {
                    Adapter._writeSlim.Release();
                }
            }
        }
        
        

        public async Task DataWriter()
        {
            while (Connected)
            {
                var (id, seg) = await _writeChannel.Reader.ReadAsync();
                if (_fibers.ContainsKey(id) && _fibers[id].IsReceivingData)
                {
                    await DataAdapter.WriteInt(id, false);
                    await DataAdapter.WriteByteArray(seg, false);
                }
                Adapter._arrayPool.Return(seg.Array);
            }
        }
        
        public async Task DataReader()
        {
            while (Connected)
            {
                var fiberId = await DataAdapter.ReadInt(false);
                var val = await DataAdapter.ReadByteArrayFast(false);
                try
                {
                    if (fiberId == -1)
                    {
                        // remove fiber
                        var id = BitConverter.ToInt32(val);
                        _disconnectQueue.Enqueue(id);
                        continue;
                    }
                    // check if the fiber is connected, if it's not just discard the packet as it might have been buffered slightly
                    if (_fibers.ContainsKey(fiberId) && _fibers[fiberId].Connected)
                    {
                        if (!_fibers[fiberId].Send(val))
                        {
                            await RemoveFiberInternal(fiberId);
                        }
                    }
                }
                catch (Exception e)
                {
                    $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                    await RemoveFiberInternal(fiberId);
                }
                finally
                {
                    DataAdapter.ReturnByte(val.Array);
                }
            }
        }
        /// <summary>
        /// The main routing function that handles all packets and responses
        /// </summary>
        private async Task Router()
        {
            while (Connected)
            {
                await Adapter._readSlim.WaitAsync();
                // read packet type
                PacketType packet = (PacketType) await Adapter.ReadInt(false);
                if (packet == PacketType.Heartbeat)
                {
                    // update heartbeat time
                    _lastHeartBeat = DateTime.Now;
                }
                else if (packet == PacketType.AddFiber)
                {
                    var id = await Adapter.ReadInt(false);
                    await _connectQueue.Writer.WriteAsync(id);
                }
                else if (packet == PacketType.FiberNotAdded)
                {
                    var id = await Adapter.ReadInt(false);
                    if (_fibers.ContainsKey(id))
                    {
                        RemoveFiber(id, true);
                    }
                }
                else if (packet == PacketType.FiberNotAccepting)
                {
                    var id = await Adapter.ReadInt(false);
                    if (_fibers.ContainsKey(id))
                    {
                        _fibers[id].IsReceivingData = false;
                    }
                }
                else if (packet == PacketType.FiberAdded)
                {
                    var id = await Adapter.ReadInt(false);
                    if (_fibers.ContainsKey(id))
                    {
                        _fibers[id].StartAcceptingData();
                    }
                }
                else if (packet == PacketType.DisconnectPacket)
                {
                    var reason = (DisconnectReason)await Adapter.ReadInt(false);
                    // Handle disconnection
                    RemoteDisconnect(reason);
                }
                else
                {
                    $"Client Conduit has been De-Synchronized".Log(LogLevel.Error);
                }

                Adapter._readSlim.Release();
                await Task.Delay(10);
            }
        }

        internal async ValueTask ForwardData(int id, ArraySegment<byte> data)
        {
            //$"FWD: {string.Join(", ", data)}".Log(LogLevel.Trace);
            var res = await _writeChannel.Writer.WaitToWriteAsync();
            if (res)
            {
                await _writeChannel.Writer.WriteAsync((id, data));
            }
        }

        private readonly SemaphoreSlim _disconnectionSlim = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Disconnect and remove the fiber from the conduit (Queued)
        /// </summary>
        /// <param name="id"></param>
        /// <param name="remote"></param>
        public void RemoveFiber(int id, bool remote = false)
        {
            while (!DispatchQueue.Writer.TryWrite(() => RemoveFiberInternal(id, remote)))
            {
                // wait for the dispatch queue to be able to write
            }
        }
        private async Task RemoveFiberInternal(int id, bool remote = false)
        {
            try
            {
                await _disconnectionSlim.WaitAsync();
                if (_fibers.ContainsKey(id))
                {
                    if (_fibers[id].Connected)
                    {
                        _fibers[id].Disconnect();
                        if (!remote)
                        {
                            await ForwardData(-1, BitConverter.GetBytes(id));
                            SendPacket(new SignalPacket(PacketType.FiberNotAccepting, id));
                        }
                    }
                    _fibers.Remove(id, out _);
                }
            }
            finally
            {
                _disconnectionSlim.Release();
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
            while (!DispatchQueue.Writer.TryWrite(() => CreateFiberInternal(fiber)))
            {
                // wait for the dispatch queue to be able to write
            }
        }
        private async Task CreateFiberInternal(Fiber fiber)
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
        /// <summary>
        /// A generic method the send data to the remote party
        /// </summary>
        /// <param name="packet"></param>
        private void SendPacket(IPacket packet)
        {
            DispatchQueue.Writer.WriteAsync(async () =>
            {
                await Adapter.WriteInt((int)packet.PacketId, false);
                await packet.SendPacket(Adapter);
            }).GetAwaiter().GetResult();
        }
    }
}
