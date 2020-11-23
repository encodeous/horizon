using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Nito.AsyncEx.Synchronous;

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

        public Fiber(Socket remote, byte[] buffer, Conduit conduit)
        {
            _buffer = buffer;
            _bufferSize = buffer.Length;
            Connected = false;
            Remote = remote;
            _hConduit = conduit;
        }

        public void StartAcceptingData()
        {
            Connected = true;
            Remote.BeginReceive(_buffer, 0, _bufferSize, SocketFlags.None, ReadCallback, Remote);
        }

        public void ReadCallback(IAsyncResult ar)
        {
            try
            {
                var sock = (Socket) ar.AsyncState;
                if (sock == null || !sock.Connected)
                {
                    //_hConduit.RemoveFiber(Id).WaitAndUnwrapException();
                    return;
                }
                int bytesRead = sock.EndReceive(ar);
                if (!sock.Connected || bytesRead == 0)
                {
                    //_hConduit.RemoveFiber(Id).WaitAndUnwrapException();
                    return;
                }
                _hConduit.ForwardData(Id, new ArraySegment<byte>(_buffer, 0, bytesRead)).WaitAndUnwrapException();
                sock.BeginReceive(_buffer, 0, _bufferSize, SocketFlags.None, ReadCallback, sock);
            }
            catch
            {
                //_hConduit.RemoveFiber(Id).WaitAndUnwrapException();
            }
        }
        public bool Send(ArraySegment<byte> data)
        {
            try
            {
                int total = data.Count;
                int sent = 0;
                while (sent < total)
                {
                    int len = Remote.Send(data.Slice(sent));
                    sent += len;
                }
            }
            catch
            {
                //_hConduit.RemoveFiber(Id).WaitAndUnwrapException();
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
                    Remote.Close();
                }
                catch
                {

                }
            }
            _hConduit.Adapter._arrayPool.Return(_buffer);
            Connected = false;
        }
    }
}
