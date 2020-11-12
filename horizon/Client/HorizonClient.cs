using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using horizon.Transport;
using wstreamlib;

namespace horizon.Client
{
    /// <summary>
    /// Allows for proxying of TCP connections to and from the server
    /// </summary>
    public class HorizonClient
    {
        private HorizonClientConfig _config;
        /// <summary>
        /// Configure the client
        /// </summary>
        /// <param name="config"></param>
        public HorizonClient(HorizonClientConfig config)
        {
            _config = config;
        }
        /// <summary>
        /// Start and Connect the client to the server
        /// </summary>
        /// <returns>Returns true if the client was successfully connected</returns>
        public bool Start()
        {
            // Start a WStream client
            var wstr = new WStream();
            // Connect the wstream client
            var wsconn = wstr.Connect(_config.Server, CancellationToken.None);
            // Check if the security handshake is successful
            if (SecurityHandshake(wsconn))
            {
                if (_config.ProxyConfig is HorizonReverseProxyConfig rproxcfg)
                {

                }
                else if (_config.ProxyConfig is HorizonProxyConfig proxcfg)
                {

                }
            }

            return false;
        }

        private bool SecurityHandshake(WsConnection wsc)
        {
            var adp = new BinaryAdapter(wsc);

            return false;
        }
    }
}
