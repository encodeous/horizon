using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using horizon.Protocol;

namespace horizon.Transport
{
    /// <summary>
    /// A class that can read from a transformed binary stream
    /// </summary>
    public class BinaryAdapter
    {
        private readonly HorizonTransformers _hTransform;

        private readonly ArrayPool<byte> _arrayPool;

        // Synchronization

        internal object _writeLock = new object();

        internal object _readLock = new object();

        public BinaryAdapter(HorizonTransformers transform)
        {
            _hTransform = transform;
            _arrayPool = ArrayPool<byte>.Create();
        }
        public BinaryAdapter(Stream stream)
        {
            _hTransform = new HorizonTransformers(stream);
            _arrayPool = ArrayPool<byte>.Create();
        }
        /// <summary>
        /// Fill buffer with bytes from the read stream
        /// </summary>
        /// <param name="buf"></param>
        public void FillBytes(Span<byte> buf)
        {
            lock (_readLock)
            {
                int offset = 0;
                int remaining = buf.Length;
                while (remaining > 0)
                {
                    int read = _hTransform.ReadStream.Read(buf.Slice(offset));
                    if (read <= 0)
                        throw new EndOfStreamException("Unexpected End Of Stream in Binary Adapter");
                    remaining -= read;
                    offset += read;
                }
            }
        }

        private byte[] IReadBytes(int bytes)
        {
            lock (_readLock)
            {
                var buf = _arrayPool.Rent(bytes);
                FillBytes(buf.AsSpan(0, bytes));
                return buf;
            }
        }
        /// <summary>
        /// Read the exact number of bytes specified
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public byte[] ReadBytes(int bytes)
        {
            lock (_readLock)
            {
                var buf = new byte[bytes];
                FillBytes(buf);
                return buf;
            }
        }
        /// <summary>
        /// Read the next 4 bytes as an integer
        /// </summary>
        /// <returns></returns>
        public int ReadInt()
        {
            lock (_readLock)
            {
                var val = IReadBytes(4);
                int k = BitConverter.ToInt32(val);
                _arrayPool.Return(val);
                return k;
            }
        }
        /// <summary>
        /// Read a byte array by first reading the length (4 bytes) then read the actual array
        /// </summary>
        /// <returns></returns>
        public byte[] ReadByteArray()
        {
            lock (_readLock)
            {
                int len = ReadInt();
                return ReadBytes(len);
            }
        }
        /// <summary>
        /// Read a byte array by first reading the length (4 bytes) then read the actual array, the returned array might be larger than the actual array.
        /// </summary>
        /// <returns></returns>
        public (byte[] arr, int len) ReadByteArrayFast()
        {
            lock (_readLock)
            {
                int len = ReadInt();
                return (IReadBytes(len), len);
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
        public long ReadLong()
        {
            lock (_readLock)
            {
                var val = IReadBytes(8);
                long k = BitConverter.ToInt64(val);
                _arrayPool.Return(val);
                return k;
            }
        }
        /// <summary>
        /// Write binary data directly to the stream
        /// </summary>
        /// <param name="buf"></param>
        public void WriteBytes(Span<byte> buf)
        {
            lock (_writeLock)
                _hTransform.WriteStream.Write(buf);
        }
        /// <summary>
        /// Write a int to the stream
        /// </summary>
        /// <param name="val"></param>
        public void WriteInt(int val)
        {
            lock (_writeLock)
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

                _hTransform.WriteStream.Write(buf, 0, 4);
                _arrayPool.Return(buf);
            }
        }
        /// <summary>
        /// Write the length of the buffer and then write the buffer
        /// </summary>
        /// <param name="buf"></param>
        public void WriteByteArray(Span<byte> buf)
        {
            lock (_writeLock)
            {
                WriteInt(buf.Length);
                WriteBytes(buf);
            }
        }
        /// <summary>
        /// Write a long to the stream
        /// </summary>
        /// <param name="val"></param>
        public void WriteLong(long val)
        {
            lock (_writeLock)
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
                _hTransform.WriteStream.Write(buf, 0, 8);
                _arrayPool.Return(buf);
            }
        }
        /// <summary>
        /// Flush the stream and any underlying streams
        /// </summary>
        public void Flush()
        {
            _hTransform.WriteStream.Flush();
        }
        /// <summary>
        /// Close the stream and any underlying streams
        /// </summary>
        public void Close()
        {
            _hTransform.WriteStream.Close();
            _hTransform.ReadStream.Close();
        }
    }
}
