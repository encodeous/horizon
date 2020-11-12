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
        /// Where the proxy server should listen to
        /// </summary>
        public IPEndPoint ReverseEndPoint;
        /// <summary>
        /// Where the proxy server should connect to
        /// </summary>
        public IPEndPoint LocalEndPoint;
    }
}
