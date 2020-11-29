using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace horizon.Client
{
    /// <summary>
    /// Configuration for normal proxying
    /// </summary>
    public class HorizonProxyConfig : IHorizonTunnelConfig
    {
        /// <summary>
        /// Local Port to Listen to
        /// </summary>
        public int LocalPort { get; set; }

        /// <summary>
        /// Where to connect to
        /// </summary>
        public EndPoint RemoteEndPoint { get; set; }
    }
}
