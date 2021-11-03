using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace horizon.Client
{
    /// <summary>
    /// An internal class that stores the connection handshake sent by the client
    /// </summary>
    class ClientConnectRequest
    {
        /// <summary>
        /// Type of Client Connection (NOTE: Remember to update this when adding new types in the future)
        /// </summary>
        public enum ConnectType
        {
            Proxy,
            ReverseProxy
        }
        public ConnectType CType { get; set; }
        /// <summary>
        /// Proxying: Address of the target, to be resolved by the server
        /// </summary>
        public string ProxyAddress { get; set; }
        /// <summary>
        /// Proxying: Port of the target
        /// </summary>
        public int ProxyPort { get; set; }
        /// <summary>
        /// Reverse Proxy: What port to listen to
        /// </summary>
        public int ListenPort { get; set; }
    }
}
