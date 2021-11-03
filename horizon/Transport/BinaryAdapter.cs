using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using wstream;

namespace horizon.Transport
{
    /// <summary>
    /// A class that can read from a binary stream
    /// </summary>
    public class BinaryAdapter
    {
        private WsStream _connection;

        internal readonly ArrayPool<byte> _arrayPool;

        // Synchronization

        internal SemaphoreSlim _readSlim = new SemaphoreSlim(1, 1);
        internal SemaphoreSlim _writeSlim = new SemaphoreSlim(1, 1);

        public BinaryAdapter(WsStream connection)
        {
            _connection = connection;
            _arrayPool = ArrayPool<byte>.Create();
        }

        /// <summary>
        /// Fill buffer with bytes from the read stream
        /// </summary>
        /// <param name="buf"></param>
        public async ValueTask FillBytes(ArraySegment<byte> buf, bool l = true)
        {
            if (!l)
            {
                int offset = 0;
                int remaining = buf.Count;
                while (remaining > 0)
                {
                    int read = await _connection.ReadAsync(buf.Slice(offset));
                    if (read <= 0)
                        throw new EndOfStreamException("Unexpected End Of Stream in Binary Adapter");
                    remaining -= read;
                    offset += read;
                }
                return;
            }
            try
            {
                await _readSlim.WaitAsync();
                int offset = 0;
                int remaining = buf.Count;
                while (remaining > 0)
                {
                    int read = await _connection.ReadAsync(buf.Slice(offset));
                    if (read <= 0)
                        throw new EndOfStreamException("Unexpected End Of Stream in Binary Adapter");
                    remaining -= read;
                    offset += read;
                }
            }
            finally
            {
                _readSlim.Release();
            }
        }
    

        private async ValueTask<byte[]> IReadBytes(int bytes, bool l = true)
        {
            if (!l)
            {
                var buf = _arrayPool.Rent(bytes);
                await FillBytes(new ArraySegment<byte>(buf, 0, bytes), false);
                return buf;
            }
            try
            {
                await _readSlim.WaitAsync();
                var buf = _arrayPool.Rent(bytes);
                await FillBytes(new ArraySegment<byte>(buf, 0, bytes), false);
                return buf;
            }
            finally
            {
                _readSlim.Release();
            }
        }
        /// <summary>
        /// Read the exact number of bytes specified
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public async ValueTask<byte[]> ReadBytes(int bytes, bool l = true)
        {
            if (!l)
            {
                var buf = new byte[bytes];
                await FillBytes(new ArraySegment<byte>(buf), false);
                return buf;
            }
            try
            {
                await _readSlim.WaitAsync();
                var buf = new byte[bytes];
                await FillBytes(new ArraySegment<byte>(buf), false);
                return buf;
            }
            finally
            {
                _readSlim.Release();
            }
        }
        /// <summary>
        /// Read the next 4 bytes as an integer
        /// </summary>
        /// <returns></returns>
        public async ValueTask<int> ReadInt(bool l = true)
        {
            if (!l)
            {
                var val = await IReadBytes(4, false);
                int k = BitConverter.ToInt32(val);
                _arrayPool.Return(val);
                return k;
            }

            try
            {
                await _readSlim.WaitAsync();
                var val = await IReadBytes(4, false);
                int k = BitConverter.ToInt32(val);
                _arrayPool.Return(val);
                return k;
            }
            finally
            {
                _readSlim.Release();
            }
        }
        /// <summary>
        /// Read a byte array by first reading the length (4 bytes) then read the actual array
        /// </summary>
        /// <returns></returns>
        public async ValueTask<byte[]> ReadByteArray(bool l = true)
        {
            if (!l)
            {
                int len = await ReadInt(false);
                return await ReadBytes(len, false);
            }
            try
            {
                await _readSlim.WaitAsync();
                int len = await ReadInt(false);
                return await ReadBytes(len, false);
            }
            finally
            {
                _readSlim.Release();
            }
        }
        /// <summary>
        /// Read a byte array by first reading the length (4 bytes) then read the actual array, the returned array might be larger than the actual array.
        /// </summary>
        /// <returns></returns>
        public async ValueTask<ArraySegment<byte>> ReadByteArrayFast(bool l = true)
        {
            if (!l)
            {
                int len = await ReadInt(false);
                return new ArraySegment<byte>(await IReadBytes(len, false), 0, len);
            }
            try
            {
                await _readSlim.WaitAsync();
                int len = await ReadInt(false);
                return new ArraySegment<byte>(await IReadBytes(len, false), 0, len);
            }
            finally
            {
                _readSlim.Release();
            }
        }
        /// <summary>
        /// Return allocated memory to the array pool
        /// </summary>
        /// <param name="arr"></param>
        public void ReturnByte(byte[] arr)
        {
            _arrayPool.Return(arr);
        }
        /// <summary>
        /// Read the next 8 bytes as a long
        /// </summary>
        /// <returns></returns>
        public async ValueTask<long> ReadLong(bool l = true)
        {
            if (!l)
            {
                var val = await IReadBytes(8, false);
                long k = BitConverter.ToInt64(val);
                _arrayPool.Return(val);
                return k;
            }
            try
            {
                await _readSlim.WaitAsync();
                var val = await IReadBytes(8, false);
                long k = BitConverter.ToInt64(val);
                _arrayPool.Return(val);
                return k;
            }
            finally
            {
                _readSlim.Release();
            }
        }
        /// <summary>
        /// Write binary data directly to the stream
        /// </summary>
        /// <param name="buf"></param>
        public async ValueTask WriteBytes(ArraySegment<byte> buf, bool l = true)
        {
            if (!l)
            {
                await _connection.WriteAsync(buf);
                return;
            }
            try
            {
                await _writeSlim.WaitAsync();
                await _connection.WriteAsync(buf);
            }
            finally
            {
                _writeSlim.Release();
            }
        }
        /// <summary>
        /// Write a int to the stream
        /// </summary>
        /// <param name="val"></param>
        public async ValueTask WriteInt(int val, bool l = true)
        {
            if (!l)
            {
                var buf = _arrayPool.Rent(4);
                if (BitConverter.IsLittleEndian)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(buf, val);
                }
                else
                {
                    BinaryPrimitives.WriteInt32BigEndian(buf, val);
                }

                await _connection.WriteAsync(new ArraySegment<byte>(buf, 0, 4));
                _arrayPool.Return(buf);
                return;
            }
            try
            {
                await _writeSlim.WaitAsync();
                var buf = _arrayPool.Rent(4);
                if (BitConverter.IsLittleEndian)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(buf, val);
                }
                else
                {
                    BinaryPrimitives.WriteInt32BigEndian(buf, val);
                }

                await _connection.WriteAsync(new ArraySegment<byte>(buf, 0, 4));
                _arrayPool.Return(buf);
            }
            finally
            {
                _writeSlim.Release();
            }
        }
        /// <summary>
        /// Write the length of the buffer and then write the buffer
        /// </summary>
        /// <param name="buf"></param>
        public async ValueTask WriteByteArray(ArraySegment<byte> buf, bool l = true)
        {
            if (!l)
            {
                await WriteInt(buf.Count, false);
                await WriteBytes(buf, false);
                return;
            }
            try
            {
                await _writeSlim.WaitAsync();
                await WriteInt(buf.Count, false);
                await WriteBytes(buf, false);
            }
            finally
            {
                _writeSlim.Release();
            }
        }
        /// <summary>
        /// Write a long to the stream
        /// </summary>
        /// <param name="val"></param>
        public async ValueTask WriteLong(long val, bool l = true)
        {
            if (!l)
            {
                var buf = _arrayPool.Rent(8);
                if (BitConverter.IsLittleEndian)
                {
                    BinaryPrimitives.WriteInt64LittleEndian(buf, val);
                }
                else
                {
                    BinaryPrimitives.WriteInt64BigEndian(buf, val);
                }
                await _connection.WriteAsync(new ArraySegment<byte>(buf, 0, 8));
                _arrayPool.Return(buf);
                return;
            }
            try
            {
                await _writeSlim.WaitAsync();
                var buf = _arrayPool.Rent(8);
                if (BitConverter.IsLittleEndian)
                {
                    BinaryPrimitives.WriteInt64LittleEndian(buf, val);
                }
                else
                {
                    BinaryPrimitives.WriteInt64BigEndian(buf, val);
                }
                await _connection.WriteAsync(new ArraySegment<byte>(buf, 0, 8));
                _arrayPool.Return(buf);
            }
            finally
            {
                _writeSlim.Release();
            }
        }

        public async ValueTask<string> ReadString()
        {
            var arr = await ReadByteArray();
            return Encoding.UTF8.GetString(arr);
        }
        
        public ValueTask WriteString(string s)
        {
            return WriteByteArray(Encoding.UTF8.GetBytes(s));
        }
    }
}
