using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
            _wsServer.Listen(_config.Bind);
            new Thread(AcceptThread);
        }

        private void AcceptThread()
        {
            while (true)
            {
                var client = _wsServer.AcceptConnectionAsync().Result;
                if (SecurityCheck(client))
                {

                }
            }
        }

        private bool SecurityCheck(WsConnection conn)
        {
            return false;
        }
    }
}
