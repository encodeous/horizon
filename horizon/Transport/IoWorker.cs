using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using wstreamlib;

namespace horizon.Transport
{
    internal class IoWorker
    {
        private ArraySegment<byte> _privateAB;
        private ArraySegment<byte> _privateBA;
        private bool IsRunning;
        private CancellationToken token;
        private WsConnection conn_a;
        private Socket conn_b;
        private HorizonRequest request;

        public IoWorker(HorizonOptions opt, WsConnection wstream, Socket sock, HorizonRequest requestInfo, CancellationToken cancellationToken)
        {
            _privateAB = new ArraySegment<byte>(new byte[opt.DefaultBufferSize]);
            _privateBA = new ArraySegment<byte>(new byte[opt.DefaultBufferSize]);
            conn_a = wstream;
            conn_b = sock;
            token = cancellationToken;
            request = requestInfo;
        }

        public delegate void WorkerStoppedDelegate(WsConnection connection, HorizonRequest request);

        public WorkerStoppedDelegate StoppedCallback;

        public void Start()
        {
            IsRunning = true;

            new Task(AtoB, token, TaskCreationOptions.LongRunning).Start();
            new Task(BtoA, token, TaskCreationOptions.LongRunning).Start();
        }

        private void AtoB()
        {
            try
            {
                while (IsRunning && !token.IsCancellationRequested)
                {
                    int len = conn_a.Read(_privateAB);
                    if (len == 0) break;

                    int sentLength = 0;
                    while (sentLength < len && conn_b.Connected)
                    {
                        int wlen = conn_b.Send(_privateAB.Slice(0, len));
                        sentLength += wlen;
                    }
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                conn_a.Close();
            }
            catch
            {
                // ignored
            }

            try
            {
                conn_b.Close();
            }
            catch
            {
                // ignored
            }

            IsRunning = false;
        }

        private void BtoA()
        {
            try
            {
                while (IsRunning && !token.IsCancellationRequested)
                {
                    int len = conn_b.Receive(_privateBA);
                    if (len == 0) break;

                    conn_a.Write(_privateBA.Slice(0, len), token);
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                conn_b.Close();
            }
            catch
            {
                // ignored
            }
            try
            {
                conn_a.Close();
            }
            catch
            {
                // ignored
            }

            StoppedCallback(conn_a, request);

            IsRunning = false;
        }
    }
}
