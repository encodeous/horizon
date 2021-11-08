using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace horizon.Server
{
    /// <summary>
    /// Server configuration
    /// </summary>
    public class HorizonServerConfig
    {
        /// <summary>
        /// Client/Server authentication token
        /// </summary>
        public string Token { get; set; } = "default";
        /// <summary>
        /// Specifies whether the remote address filter is on whitelist or blacklist
        /// </summary>
        public bool Whitelist { get; set; } = true;

        /// <summary>
        /// A set of Hosts that a client is allowed/denied from connecting to
        /// </summary>
        public RemotePattern[] RemotesPattern { get; set; } =
            { new RemotePattern() { HostRegex = "[\\s\\S]", PortRangeStart = 1, PortRangeEnd = 65535 } };

        /// <summary>
        /// A set of ports that a client is allowed/denied from binding to as their reverse proxy.
        /// </summary>
        public int[] ReverseBinds { get; set; } = new[] { 8080 };

        /// <summary>
        /// Horizon's local Port and IP Address. Use the format <code>ipaddress:port</code>
        /// </summary>
        public string Bind { get; set; } = "0.0.0.0:5050";
    }
}
