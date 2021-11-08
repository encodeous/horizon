using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using horizon.Threading;
using Microsoft.Extensions.Logging;

namespace horizon.Transport
{
    public partial class Fiber
    {
        public async ValueTask Receiver()
        {
            try
            {
                while (Connected)
                {
                    if (!_highPerf)
                    {
                        if(!IsReadingData) _releaseBackpressure.Reset();
                        await _releaseBackpressure.WaitHandle.AsTask(Timeout.InfiniteTimeSpan);
                    }
                    var len = await Remote.ReceiveAsync(_currentBuffer, SocketFlags.None);
                    if (len == 0)
                    {
                        break;
                    }
                    await _hConduit.ForwardData(Id, new ArraySegment<byte>(_currentBuffer, 0, len));
                    if (!_highPerf)
                    {
                        lock (_remoteBufferLock)
                        {
                            _remoteBufferRem++;
                            if(_remoteBufferRem >= BackpressureStopLimit)
                            {
                                CrossedStopLimit = true;
                                IsReadingData = false;
                            }
                        }
                    }
                    _currentBuffer = ArrayPool<byte>.Shared.Rent(_packetSize);
                }
            }
            catch(Exception e)
            {
                $"Exception occurred while reading from fiber {Id}: {e.Message} {e.StackTrace}".Log(LogLevel.Trace);
            }
            
            _hConduit.RemoveFiber(Id);
            Remote.Dispose();
        }
        
        /// <summary>
        /// A dedicated thread that sends data to the tcp remote, only used in low-performance mode
        /// </summary>
        private async Task Sender()
        {
            while (Connected)
            {
                var buf = await FiberBuffer.Reader.ReadAsync();
                try
                {
                    await _writeStream.WriteAsync(buf);
                    var b = ArrayPool<byte>.Shared.Rent(8);
                    var a = BitConverter.GetBytes(Id);
                    var c = BitConverter.GetBytes(FiberBuffer.Reader.Count);
                    for (int i = 0; i < 4; i++)
                    {
                        b[i] = a[i];
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        b[i+4] = c[i];
                    }
                    await _hConduit.ForwardData(-2, new ArraySegment<byte>(b, 0, 8));
                }
                catch(Exception e)
                {
                    $"Exception occurred while writing to fiber: {e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                    _hConduit.RemoveFiber(Id);
                }
                finally
                {
                    _hConduit.DataAdapter.ReturnByte(buf.Array);
                }
            }
        }
        
        /// <summary>
        /// Send data directly to the socket, only used in high performance mode
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        internal ValueTask Send(ArraySegment<byte> data)
        {
            try
            {
                return _writeStream.WriteAsync(data);
            }
            catch(Exception e)
            {
                $"Exception occurred while writing to fiber: {e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                _hConduit.RemoveFiber(Id);
            }
            return ValueTask.CompletedTask;
        }
    }
}