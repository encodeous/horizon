using System;
using System.Collections.Generic;
using System.Text;

namespace horizon.Server
{
    /// <summary>
    /// Stores pattern matching information about the remote
    /// </summary>
    public struct RemotePattern
    {
        /// <summary>
        /// A regex pattern to match the host the client is allowed to proxy to
        /// </summary>
        public string HostRegex;
        /// <summary>
        /// A port denoting the start of the allowed range
        /// </summary>
        public int PortRangeStart;
        /// <summary>
        /// A port denoting the end of the allowed range, inclusive.
        /// </summary>
        public int PortRangeEnd;
    }
}
