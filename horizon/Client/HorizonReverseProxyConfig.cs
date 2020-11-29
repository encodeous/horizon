using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace horizon.Client
{
    /// <summary>
    /// Configuration for Reverse Proxying
    /// </summary>
    public class HorizonReverseProxyConfig : IHorizonTunnelConfig
    {
        /// <summary>
        /// Where the reverse proxy server should listen to
        /// </summary>
        public int ListenPort { get; set; }
        /// <summary>
        /// Where the reverse proxy server should connect to
        /// </summary>
        public IPEndPoint LocalEndPoint { get; set; }
    }
}
