using System;
using System.Buffers;
using System.Threading.Tasks;
using horizon.Packets;
using horizon.Threading;
using Microsoft.Extensions.Logging;

namespace horizon.Transport
{
    public partial class Conduit
    {
        public async Task DataWriter()
        {
            while (Connected)
            {
                try
                {
                    var (id, seg) = await _writeChannel.Reader.ReadAsync();
                    if (id < 0 || _fibers.ContainsKey(id) && _fibers[id].IsReceivingData)
                    {
                        await DataAdapter.WriteInt(id, false);
                        await DataAdapter.WriteByteArray(seg, false);
                    }
                    if(seg.Array != null) 
                        ArrayPool<byte>.Shared.Return(seg.Array);
                }
                catch (Exception e)
                {
                    $"Error occurred in DataWriter: {e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                }
            }
        }
        
        private async Task DataReader()
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
                        RemoveFiber(id);
                        continue;
                    }
                    if (fiberId == -2)
                    {
                        // backpressure update
                        var id = BitConverter.ToInt32(val[..4]);
                        var pressure = BitConverter.ToInt32(val[4..8]);
                        if (_fibers.ContainsKey(id))
                        {
                            _fibers[id].BackpressureUpdate(pressure);
                        }
                    }
                    // check if the fiber is connected, if it's not just discard the packet as it might have been buffered slightly
                    if (_fibers.ContainsKey(fiberId) && _fibers[fiberId].Connected)
                    {
                        if (HighPerf)
                        {
                            await _fibers[fiberId].Send(val);
                        }
                        else
                        {
                            if (!_fibers[fiberId].FiberBuffer.Writer.TryWrite(val))
                            {
                                // the buffer was full, but the remote kept sending data
                                // we must assume that the remote is not following the protocol and is sending data faster than we can handle
                                RemoveFiber(fiberId);
                            }
                            else
                            {
                                if (_fibers[fiberId].FiberBuffer.Reader.Count >= Fiber.BackpressureStopLimit)
                                {
                                    _fibers[fiberId].CrossedStopLimit = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    $"Error occurred in DataReader: {e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                    RemoveFiber(fiberId);
                }
            }
        }
        /// <summary>
        /// The main routing function that handles all packets and responses
        /// </summary>
        private async Task Router()
        {
            while (Connected && _wsConn.Connected)
            {
                try
                {
                    // read packet type
                    PacketType packet = (PacketType) await Adapter.ReadInt(false);
                    if (packet == PacketType.Heartbeat)
                    {
                        // update heartbeat time
                        $"Received heartbeat".Log(LogLevel.Trace);
                        _lastHeartBeat = DateTime.Now;
                    }
                    else if (packet == PacketType.AddFiber)
                    {
                        var id = await Adapter.ReadInt(false);
                        ActionDispatch.Add(()=> InitializeFiber(id), Dispatcher.Priority.Slow);
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
                            ActionDispatch.Add(() => { _fibers[id].StartAcceptingData(); return ValueTask.CompletedTask;});
                        }
                    }
                    else if (packet == PacketType.DisconnectPacket)
                    {
                        var reason = (DisconnectReason) await Adapter.ReadInt(false);
                        // Handle disconnection
                        RemoteDisconnect(reason);
                        break;
                    }
                    else
                    {
                        $"Client Conduit has been De-Synchronized".Log(LogLevel.Error);
                    }
                }
                catch (Exception e)
                {
                    await Disconnect(DisconnectReason.Terminated);
                    $"Error occurred in Router: {e.Message} {e.StackTrace}".Log(LogLevel.Debug);
                }
            }

            Connected = false;
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
        
        /// <summary>
        /// A generic method the send data to the remote party
        /// </summary>
        /// <param name="packet"></param>
        internal void SendPacket(IPacket packet)
        {
            ActionDispatch.Add(async () =>
            {
                await Adapter.WriteInt((int)packet.PacketId, false);
                await packet.SendPacket(Adapter);
            }, Dispatcher.Priority.Live);
        }
    }
}