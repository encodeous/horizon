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
        public string Token { get; set; }
        /// <summary>
        /// Horizon Server address
        /// </summary>
        public Uri Server { get; set; }
        /// <summary>
        /// Proxy configuration, either use <see cref="HorizonProxyConfig"/> or <see cref="HorizonReverseProxyConfig"/>
        /// </summary>
        public IHorizonTunnelConfig ProxyConfig { get; set; }
    }
}
