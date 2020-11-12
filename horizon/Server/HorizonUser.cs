using System;
using System.Collections.Generic;
using System.Text;

namespace horizon.Server
{
    public class HorizonUser
    {
        /// <summary>
        /// Specifies whether the permission mode is white-list match or blacklist match
        /// </summary>
        public bool Whitelist = false;
        /// <summary>
        /// A set of IP addresses that is allowed to connect to horizon (Regex)
        /// </summary>
        public HashSet<string> ClientIp;
        /// <summary>
        /// A set of Hosts a client is allowed to connect to, or hosts that the reverse proxy is allowed to bind to
        /// </summary>
        public HashSet<(string hostRegex, int port, bool reverseProxy)> RemotesRegex;
    }
}
