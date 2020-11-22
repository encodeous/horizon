using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using horizon.Packets;
using horizon.Protocol;
using wstreamlib;

namespace horizon.Transport
{
    /// <summary>
    /// A class that handles the routing and transport of "Fiber" connections
    /// </summary>
    public class Conduit
    {
        private readonly BinaryAdapter _adapter;
        /// <summary>
        /// Checks if the Tunnel is still connected
        /// </summary>
        public bool Connected { get; private set; }
        /// <summary>
        /// Checks if the Tunnel is using Encryption
        /// </summary>
        public bool Encrypted { get; }
        /// <summary>
        /// Last heartbeat packet received
        /// </summary>
        private DateTime _lastHeartBeat = DateTime.Now;
        /// <summary>
        /// Encryption Signature, used for debugging
        /// </summary>
        public byte[] Signature { get; }
        private readonly Fiber[] _fibers;
        private readonly WsConnection _wsConn;

        public delegate void DisconnectDelegate(DisconnectReason reason);
        /// <summary>
        /// Called on Disconnection of the Tunnel
        /// </summary>
        public event DisconnectDelegate OnDisconnect;

        public delegate Fiber CreateFiberCallback();
        /// <summary>
        /// Called on Creation of a new Fiber by the Remote
        /// </summary>
        public CreateFiberCallback FiberCreate;

        public Conduit(WsConnection rawWs, string token, bool useEncryption = false, int timeoutMilliseconds = 30000, int maxFibers = 1000)
        {
            _wsConn = rawWs;
            _fibers = new Fiber[maxFibers];
            Encrypted = useEncryption;
            // Wrap the binary stream for easier read/write
            var transformer = new HorizonTransformers(rawWs);
            _adapter = new BinaryAdapter(transformer);

            if (useEncryption)
            {
                // Establish an Encrypted Connection
                var enc = new EncryptionTransformer(token);
                // Broadcast Encryption Packet
                SendPacket(new PublicKeyBroadcast(){PublicKey = enc.GetPublicKey()});
                int id = _adapter.ReadInt();
                if (id != (int) PacketType.PublicKeyBroadcast)
                {
                    // Unexpected packet received
                    throw new IOException("Expected Public Key Broadcast!");
                }
                // Create the shared private AES key
                enc.CompleteEncryptionHandshake(_adapter.ReadByteArray());
                // Set the Signature
                Signature = enc.Signature;
                // Modify the transformer to allow seamless encryption
                transformer.AddTransformer(enc);
            }

            Connected = true;
            // Start Routing, Local Socket Checking and Heartbeat functions
            new Thread(Router).Start();
            new Thread(FiberChecker).Start();
            Task.Run(async () =>
            {
                while (Connected)
                {
                    if (DateTime.Now - _lastHeartBeat > TimeSpan.FromMilliseconds(timeoutMilliseconds))
                    {
                        Disconnect(DisconnectReason.Timeout);
                        break;
                    }
                    SendPacket(new SignalPacket(PacketType.Heartbeat));
                    await Task.Delay(timeoutMilliseconds / 2);
                }
            });
            // Register websocket disconnection
            rawWs.ConnectionClosedEvent += RawWsOnConnectionClosedEvent;
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
                OnDisconnect?.Invoke(reason);
                try
                {
                    _wsConn.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
            Connected = false;
        }
        /// <summary>
        /// Disconnect from the remote, use <code>DisconnectReason.Terminated</code> to kill the connection
        /// </summary>
        /// <param name="reason"></param>
        public void Disconnect(DisconnectReason reason = DisconnectReason.Shutdown)
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
                                await _wsConn.DisposeAsync();
                            }
                            catch
                            {
                                // ignored
                            }
                            OnDisconnect?.Invoke(DisconnectReason.Terminated);
                        }
                        else
                        {
                            OnDisconnect?.Invoke(reason);
                        }
                    });
                }
                else
                {
                    // Kill connection
                    try
                    {
                        _wsConn.Dispose();
                    }
                    catch
                    {
                        // ignored
                    }
                    OnDisconnect?.Invoke(reason);
                }
                // Disconnect all local clients
                DisconnectFibers();
            }
            Connected = false;
        }
        /// <summary>
        /// Call to close and dispose all local Fibers and Sockets
        /// </summary>
        private void DisconnectFibers()
        {
            for (var i = 0; i < _fibers.Length; i++)
            {
                var f = _fibers[i];
                if(f == null || !f.Connected) continue; // No fiber active at current index
                try
                {
                    f.Disconnect();
                    _fibers[i] = null;
                }
                catch
                {
                    // ignored
                }
            }
        }
        /// <summary>
        /// The main routing function that handles all packets and responses
        /// </summary>
        private void Router()
        {
            while (Connected)
            {
                // read packet type
                PacketType packet = (PacketType)_adapter.ReadInt();
                if (packet == PacketType.Heartbeat)
                {
                    // update heartbeat time
                    _lastHeartBeat = DateTime.Now;
                }
                else if (packet == PacketType.AddFiber)
                {
                    var id = _adapter.ReadInt();
                    // check if id is valid, and within the allowed range
                    if (id < 0 || id > _fibers.Length) continue;
                    var res = FiberCreate?.Invoke();
                    // make sure a malicious server/client cannot create a fiber on an input node
                    if (res != null)
                    {
                        // Override existing fibers
                        if(_fibers[id] != null) _fibers[id].Disconnect(); // Disconnect the fiber if it already exists
                        _fibers[id] = res;
                        res.Connected = true;
                        // Signal that the fiber is active
                        SendPacket(new SignalPacket(PacketType.FiberAdded, id));
                    }
                    else
                    {
                        // Invalid fiber packet, or rejected
                        SendPacket(new SignalPacket(PacketType.FiberNotAdded, id));
                    }
                }
                else if (packet == PacketType.RemoveFiber)
                {
                    var id = _adapter.ReadInt();
                    if (id < 0 || id > _fibers.Length)
                    {
                        _fibers[id].Disconnect();
                        _fibers[id] = null;
                    }
                }
                else if (packet == PacketType.FiberNotAdded)
                {
                    var id = _adapter.ReadInt();
                    if (id < 0 || id > _fibers.Length)
                    {
                        _fibers[id].Disconnect();
                        _fibers[id] = null;
                    }
                }
                else if(packet == PacketType.FiberAdded)
                {
                    var id = _adapter.ReadInt();
                    if (id < 0 || id > _fibers.Length)
                    {
                        _fibers[id].Connected = true;
                    }
                }
                else if (packet == PacketType.DataPacket)
                {
                    // prevent mixed up packets
                    lock (_adapter._readLock)
                    {
                        // do not wrap data packet in object for better performance
                        var fiberId = _adapter.ReadInt();
                        if (_fibers[fiberId] != null)
                        {
                            var val = _adapter.ReadByteArrayFast();
                            // check if the fiber is connected, if it's not just discard the packet as it might have been buffered slightly
                            if(_fibers[fiberId].Connected) _fibers[fiberId].Remote.Write(val.arr, 0, val.len);
                            _adapter.ReturnByte(val.arr);
                        }
                        else
                        {
                            // a fatal error has occurred, and there is a fiber mismatch between the client and server
                            Disconnect(DisconnectReason.Terminated);
                        }
                    }
                }
                else if (packet == PacketType.DisconnectPacket)
                {
                    var reason = (DisconnectReason) _adapter.ReadInt();
                    // Handle disconnection
                    RemoteDisconnect(reason);
                }
            }
        }
        /// <summary>
        /// Checks the local fibers for data
        /// </summary>
        private void FiberChecker()
        {
            ArrayPool<byte> arrayPool = ArrayPool<byte>.Create();
            while (Connected)
            {
                for (var i = 0; i < _fibers.Length; i++)
                {
                    var f = _fibers[i];
                    try
                    {
                        // do not wrap data packet in object for better performance
                        if (f != null && f.Remote.DataAvailable && f.Connected)
                        {
                            // TODO: Add custom buffer size
                            var arr = arrayPool.Rent(4096);
                            var len = f.Remote.Read(arr);

                            lock (_adapter._writeLock)
                            {
                                _adapter.WriteInt((int)PacketType.DataPacket);
                                _adapter.WriteInt(i);
                                _adapter.WriteByteArray(arr.AsSpan(len));
                            }
                        }
                    }
                    catch
                    {
                        RemoveFiber(i);
                    }
                }
            }
        }
        /// <summary>
        /// Disconnect and remove the fiber from the conduit
        /// </summary>
        /// <param name="id"></param>
        public void RemoveFiber(int id)
        {
            try
            {
                _fibers[id].Disconnect();
                SendPacket(new SignalPacket(PacketType.RemoveFiber, id));
            }
            catch
            {

            }
            _fibers[id] = null;
        }
        /// <summary>
        /// Initiate a new fiber in the conduit
        /// </summary>
        /// <param name="fiber"></param>
        public void AddFiber(Fiber fiber)
        {
            while (true)
            {
                for (int i = 0; i < _fibers.Length; i++)
                {
                    if (_fibers[i] == null)
                    {
                        _fibers[i] = fiber;
                        SendPacket(new SignalPacket(PacketType.AddFiber, i));
                        return;
                    }
                }
            }
        }
        /// <summary>
        /// A generic method the send data to the remote party
        /// </summary>
        /// <param name="packet"></param>
        private void SendPacket(IPacket packet)
        {
            lock (_adapter)
            {
                _adapter.WriteInt((int)packet.PacketId);
                packet.SendPacket(_adapter);
            }
        }
    }
}
