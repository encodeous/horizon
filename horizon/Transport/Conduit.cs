using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using horizon.Packets;
using Microsoft.Extensions.Logging;
using wstreamlib;

namespace horizon.Transport
{
    /// <summary>
    /// A class that handles the routing and transport of "Fiber" connections
    /// </summary>
    public class Conduit
    {
        private ConcurrentQueue<int> _disconnectQueue = new ConcurrentQueue<int>();
        private ConcurrentQueue<int> _connectQueue = new ConcurrentQueue<int>();

        internal readonly BinaryAdapter Adapter;
        /// <summary>
        /// Checks if the Tunnel is still connected
        /// </summary>
        public bool Connected { get; private set; }
        /// <summary>
        /// Last heartbeat packet received
        /// </summary>
        private DateTime _lastHeartBeat = DateTime.Now;
        private readonly ConcurrentDictionary<int, Fiber> _fibers;
        internal readonly WsConnection _wsConn;

        public delegate void DisconnectDelegate(DisconnectReason reason, Guid clientId, bool remote);
        /// <summary>
        /// Called on Disconnection of the Tunnel
        /// </summary>
        public event DisconnectDelegate OnDisconnect;

        public delegate Fiber CreateFiberCallback();
        /// <summary>
        /// Called on Creation of a new Fiber by the Remote
        /// </summary>
        public CreateFiberCallback FiberCreate;

        public Conduit(WsConnection rawWs, int timeoutMilliseconds = 5000)
        {
            _wsConn = rawWs;
            _fibers = new ConcurrentDictionary<int, Fiber>();
            // Wrap the binary stream for easier read/write
            Adapter = new BinaryAdapter(rawWs);
            Connected = true;
            // Start Routing, Local Socket Checking and Heartbeat functions
            Task.Run(Router);
            Task.Run(FiberThread);
            Task.Run(Dispatcher);
            Task.Run(()=>Heartbeat(timeoutMilliseconds));
            // Register websocket disconnection
            rawWs.ConnectionClosedEvent += RawWsOnConnectionClosedEvent;
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
                    await Disconnect(DisconnectReason.Timeout);
                    break;
                }
                SendPacket(new SignalPacket(PacketType.Heartbeat));
                await Task.Delay(timeoutMilliseconds / 2);
            }
        }
        /// <summary>
        /// Checks if the remote requested to connect or delete a fiber
        /// </summary>
        /// <returns></returns>
        private async Task FiberThread()
        {
            while (Connected)
            {
                if (_disconnectQueue.TryDequeue(out var a))
                {
                    if (_fibers.ContainsKey(a))
                    {
                        await RemoveFiber(a, true);
                    }
                } else if (_connectQueue.TryDequeue(out var id))
                {
                    var res = FiberCreate?.Invoke();
                    // make sure a malicious server/client cannot create a fiber on an input node
                    if (res != null)
                    {
                        res.Id = id;
                        // Override existing fibers
                        if (_fibers.ContainsKey(id)) await RemoveFiber(id, true);
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
                await Task.Delay(10);
            }
        }

        /// <summary>
        /// This function is called when the tunnel is abruptly closed
        /// </summary>
        /// <param name="connection"></param>
        private void RawWsOnConnectionClosedEvent(WsConnection connection) => RemoteDisconnect(DisconnectReason.Terminated);
        /// <summary>
        /// Call to signal that the Remote party has disconnected
        /// </summary>
        /// <param name="reason"></param>
        private void RemoteDisconnect(DisconnectReason reason = DisconnectReason.Shutdown)
        {
            if (Connected)
            {
                Connected = false;
                OnDisconnect?.Invoke(reason, _wsConn.ConnectionId, true);
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
                }

            }
        }
        /// <summary>
        /// Disconnect from the remote, use <code>DisconnectReason.Terminated</code> to kill the connection
        /// </summary>
        /// <param name="reason"></param>
        public async Task Disconnect(DisconnectReason reason = DisconnectReason.Shutdown)
        {
            if (Connected)
            {
                if (reason != DisconnectReason.Terminated)
                {
                    // Send graceful shutdown message
                    SendPacket(new DisconnectPacket()
                    {
                        Reason = reason
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
                                await _wsConn.Close();
                            }
                            catch(Exception e)
                            {
                                $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                            }
                            Connected = false;
                            OnDisconnect?.Invoke(DisconnectReason.Terminated, _wsConn.ConnectionId, false);
                        }
                        else
                        {
                            OnDisconnect?.Invoke(reason, _wsConn.ConnectionId, false);
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
                            await _wsConn.Close();
                        }
                        catch(Exception e)
                        {
                            $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                        }
                    }
                    OnDisconnect?.Invoke(reason, _wsConn.ConnectionId, false);
                }
            }
            Connected = false;
        }
        public ConcurrentQueue<Func<Task>> DispatchQueue = new ConcurrentQueue<Func<Task>>();
        /// <summary>
        /// Dispatches packets on a single thread to prevent a race event
        /// </summary>
        /// <returns></returns>
        private async Task Dispatcher()
        {
            while (Connected)
            {
                if (DispatchQueue.TryDequeue(out var x))
                {
                    try
                    {
                        await Adapter._writeSlim.WaitAsync();
                        await x.Invoke();
                    }
                    finally
                    {
                        Adapter._writeSlim.Release();
                    }
                }

                if (DispatchQueue.IsEmpty)
                {
                    await Task.Delay(10);
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
                // read packet type
                PacketType packet = (PacketType) await Adapter.ReadInt();
                if (packet == PacketType.Heartbeat)
                {
                    // update heartbeat time
                    _lastHeartBeat = DateTime.Now;
                }
                else if (packet == PacketType.AddFiber)
                {
                    var id = await Adapter.ReadInt();
                    _connectQueue.Enqueue(id);
                }
                else if (packet == PacketType.RemoveFiber)
                {
                    var id = await Adapter.ReadInt();
                    _disconnectQueue.Enqueue(id);
                }
                else if (packet == PacketType.FiberNotAdded)
                {
                    var id = await Adapter.ReadInt();
                    if (_fibers.ContainsKey(id))
                    {
                        await RemoveFiber(id, true);
                    }
                }
                else if(packet == PacketType.FiberAdded)
                {
                    var id = await Adapter.ReadInt();
                    if (_fibers.ContainsKey(id))
                    {
                        _fibers[id].StartAcceptingData();
                    }
                }
                else if (packet == PacketType.DataPacket)
                {
                    await Adapter._readSlim.WaitAsync();
                    var fiberId = await Adapter.ReadInt(false);
                    var val = await Adapter.ReadByteArrayFast(false);
                    try
                    {
                        // check if the fiber is connected, if it's not just discard the packet as it might have been buffered slightly
                        if (_fibers.ContainsKey(fiberId) && _fibers[fiberId].Connected)
                        {
                            if (!_fibers[fiberId].Send(val))
                            {
                                await RemoveFiber(fiberId);
                            }
                        }
                        else
                        {
                            // stray packet
                        }
                    }
                    catch(Exception e)
                    {
                        $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                        await RemoveFiber(fiberId);
                    }
                    finally
                    {
                        Adapter.ReturnByte(val.Array);
                        Adapter._readSlim.Release();
                    }
                }
                else if (packet == PacketType.DisconnectPacket)
                {
                    var reason = (DisconnectReason) await Adapter.ReadInt();
                    // Handle disconnection
                    RemoteDisconnect(reason);
                }
                else
                {
                    $"Client Conduit has been De-Synchronized".Log(LogLevel.Error);
                }
            }
        }

        internal async ValueTask ForwardData(int id, ArraySegment<byte> data)
        {
            try
            {
                await Adapter._writeSlim.WaitAsync();
                await Adapter.WriteInt((int)PacketType.DataPacket, false);
                await Adapter.WriteInt(id, false);
                await Adapter.WriteByteArray(data, false);
            }
            finally
            {
                Adapter._writeSlim.Release();
            }
        }

        private readonly SemaphoreSlim _disconnectionSlim = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Disconnect and remove the fiber from the conduit
        /// </summary>
        /// <param name="id"></param>
        /// <param name="remote"></param>
        public async Task RemoveFiber(int id, bool remote = false)
        {
            try
            {
                await _disconnectionSlim.WaitAsync();
                if (_fibers.ContainsKey(id))
                {
                    if (_fibers[id].Connected)
                    {
                        _fibers[id].Disconnect();
                        if(!remote) SendPacket(new SignalPacket(PacketType.RemoveFiber, id));
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
        public async Task AddFiber(Fiber fiber)
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
            DispatchQueue.Enqueue(async () =>
            {
                await Adapter.WriteInt((int)packet.PacketId, false);
                await packet.SendPacket(Adapter);
            });
        }
    }
}
