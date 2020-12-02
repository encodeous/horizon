using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace horizon.Server
{
    /// <summary>
    /// Stores information about a user that is allowed/disallowed to connect
    /// </summary>
    public class HorizonUser
    {
        /// <summary>
        /// Client authentication token
        /// </summary>
        public string Token { get; set; }
        /// <summary>
        /// Specifies whether the permission mode is white-list match or blacklist match
        /// </summary>
        public bool Whitelist { get; set; } = false;
        /// <summary>
        /// A set of Hosts that a client is allowed/denied from connecting to
        /// </summary>
        public RemotePattern[] RemotesPattern { get; set; } = Array.Empty<RemotePattern>();
        /// <summary>
        /// A set of ports that a client is allowed/denied from binding to as their reverse proxy.
        /// </summary>
        public int[] ReverseBinds { get; set; } = Array.Empty<int>();
    }
}
