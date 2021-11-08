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
        /// Client token, used for encryption
        /// </summary>
        public string Token { get; set; }
        /// <summary>
        /// Horizon Server address
        /// </summary>
        public Uri Server { get; set; }
        /// <summary>
        /// Set to true to disable stability features such as backpressure
        /// </summary>
        public bool HighPerformance { get; set; }
        /// <summary>
        /// Proxy configuration, either use <see cref="HorizonProxyConfig"/> or <see cref="HorizonReverseProxyConfig"/>
        /// </summary>
        public IHorizonTunnelConfig ProxyConfig { get; set; }
    }
}
