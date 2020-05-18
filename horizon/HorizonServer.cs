using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using horizon.Transport;
using wstreamlib;

namespace horizon
{
    public class HorizonServer
    {
        public IoManager ioManager;
        private WStreamServer serverInstance;
        private ConnectionValidator connectionValidator;

        private Task hAcceptThread;

        private CancellationToken _stopToken;
        private CancellationTokenSource _stopTokenSource;

        public HorizonServer(List<UserPermission> permissionInfo)
        {
            serverInstance = new WStreamServer();
            connectionValidator = ConnectionValidator.CreateServerConnectionValidator(permissionInfo);
            _stopTokenSource = new CancellationTokenSource();
            _stopToken = _stopTokenSource.Token;
        }

        /// <summary>
        /// Sets the permissions for the given user id, if it does not exist, it will create one
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="permission"></param>
        public void SetPermission(string userId, UserPermission permission)
        {
            connectionValidator.uidMap[userId] = permission;
        }

        /// <summary>
        /// Gets the user permission
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>Returns the UserPermission of the requested id, if it is not found, null is returned.</returns>
        public UserPermission GetPermission(string userId)
        {
            if (connectionValidator.uidMap.ContainsKey(userId))
            {
                return connectionValidator.uidMap[userId];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Listen on the specified endpoint
        /// </summary>
        /// <param name="endpoint"></param>
        public void Listen(IPEndPoint endpoint)
        {
            serverInstance.Listen(endpoint);
            ioManager = new IoManager(new HorizonOptions());
        }

        /// <summary>
        /// Listen on the specified endpoint with parameters
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="opt"></param>
        public void Listen(IPEndPoint endpoint, HorizonOptions opt)
        {
            serverInstance.Listen(endpoint);
            ioManager = new IoManager(opt);
        }

        /// <summary>
        /// Listen on the specified endpoint with parameters and a certificate
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="cert"></param>
        /// <param name="opt"></param>
        public void Listen(IPEndPoint endpoint, X509Certificate cert, HorizonOptions opt)
        {
            serverInstance.Listen(endpoint, cert);
            ioManager = new IoManager(opt);
        }

        /// <summary>
        /// Starts the main connections thread
        /// </summary>
        public void Start()
        {
            hAcceptThread = new Task(AcceptConnections, _stopToken, TaskCreationOptions.LongRunning);
            hAcceptThread.Start();
        }

        private async void AcceptConnections()
        {
            while (!_stopToken.IsCancellationRequested)
            {
                try
                {
                    var conn = await serverInstance.AcceptConnectionAsync().ConfigureAwait(false);
                    if (conn.Connected)
                    {
                        try
                        {
                            (bool, HorizonRequest) request = ProtocolManager.PerformServerHandshake(conn, connectionValidator);
                            if (request.Item1 && request.Item2 != null)
                            {
                                if (request.Item2.PingPacket)
                                {
                                    var time = DateTime.UtcNow - request.Item2.RequestTime;
                                    $"Client {request.Item2.UserId} has pinged. Latency {time.TotalMilliseconds} ms".Log(Logger.LoggingLevel.Debug);
                                }
                                else
                                {
                                    OpenConnection(request.Item2, conn);
                                }
                            }
                            else
                            {
                                conn.Close();
                            }
                        }
                        catch
                        {
                            conn.Close();
                        }
                    }
                }
                catch(Exception e)
                {
                    "Failed accepting client.".Log(Logger.LoggingLevel.Severe);
                    $"{e.Message} {e.StackTrace}".Log(Logger.LoggingLevel.Verbose);
                }
            }
        }

        private void OpenConnection(HorizonRequest request, WsConnection connection)
        {
            try
            {
                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                sock.Connect(request.RequestedHost, request.RequestedPort);
                ioManager.AddIoConnection(connection, sock, request);
            }
            catch(Exception e)
            {
                $"Failed to open connection to remote {request.RequestedHost}:{request.RequestedPort}".Log(Logger.LoggingLevel.Severe);
                $"{e.Message} {e.StackTrace}".Log(Logger.LoggingLevel.Verbose);
                connection.Close();
            }
        }

        public void Close()
        {
            _stopTokenSource.Cancel();
        }
    }
}
