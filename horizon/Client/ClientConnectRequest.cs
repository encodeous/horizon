using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace horizon.Client
{
    class ClientConnectRequest
    {
        public enum ConnectType
        {
            Proxy,
            ReverseProxy
        }
        public byte[] HashedBytes { get; set; }
        public ConnectType CType { get; set; }
        public EndPoint ProxyEndpoint { get; set; }
        public int ListenPort { get; set; }
    }
}
