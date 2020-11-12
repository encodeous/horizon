using System;
using System.Collections.Generic;
using System.Text;

namespace horizon.Client
{
    /// <summary>
    /// Configuration for normal proxying
    /// </summary>
    public class HorizonProxyConfig : IHorizonTunnelConfig
    {
        /// <summary>
        /// Where to connect to
        /// </summary>
        public string Remote;
        /// <summary>
        /// Remote port
        /// </summary>
        public int Port;
    }
}
