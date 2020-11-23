using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        public async Task<bool> Start()
        {
            // Start a WStream client
            var wstr = new WStream();
            // Connect the wstream client
            var wsconn = await wstr.Connect(_config.Server);
            // Check if the security handshake is successful
            if (SecurityHandshake(wsconn))
            {
                if (_config.ProxyConfig is HorizonReverseProxyConfig rproxcfg)
                {

                }
                else if (_config.ProxyConfig is HorizonProxyConfig proxcfg)
                {
                    var cd = new Conduit(wsconn);
                    var ipt = new HorizonInput(new IPEndPoint(IPAddress.Any, 500), cd);
                }
            }

            return false;
        }

        private bool SecurityHandshake(WsConnection wsc)
        {
            var adp = new BinaryAdapter(wsc);

            return true;
        }
    }
}
