using System;
using System.Collections.Generic;
using System.Text;

namespace horizon.Client
{
    /// <summary>
    /// Client configuration
    /// </summary>
    public class HorizonClientConfig
    {
        /// <summary>
        /// Client token
        /// </summary>
        public string Token;
        /// <summary>
        /// Server address
        /// </summary>
        public Uri Server;
        /// <summary>
        /// Proxy configuration, either use <see cref="HorizonProxyConfig"/> or <see cref="HorizonReverseProxyConfig"/>
        /// </summary>
        public IHorizonTunnelConfig ProxyConfig;
    }
}
