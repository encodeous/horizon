using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using horizon.Transport;
using horizon.Utilities;
using wstreamlib;

namespace horizon.Server
{
    public class HorizonServer
    {
        private HorizonServerConfig _config;
        private WStreamServer _wsServer;
        /// <summary>
        /// Configure the server
        /// </summary>
        /// <param name="config"></param>
        public HorizonServer(HorizonServerConfig config)
        {
            _config = config;
            _wsServer = new WStreamServer();
        }

        public void Start()
        {
            Task.Run(()=> _wsServer.Listen(_config.Bind));
            _wsServer.ConnectionAddedEvent += AcceptConnections;
        }

        private void AcceptConnections(WsConnection connection)
        {
            if (SecurityCheck(connection))
            {
                var cd = new Conduit(connection);
                HorizonOutput hzo = new HorizonOutput("localhost", 5000, cd);
            }
        }

        private bool SecurityCheck(WsConnection conn)
        {
            return true;
        }
    }
}
