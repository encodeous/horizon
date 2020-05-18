using System;
using System.Collections.Generic;
using System.Text;

namespace horizon
{
    public class PingResult
    {
        /// <summary>
        /// The time it took to complete the Handshake
        /// </summary>
        public TimeSpan Latency;

        /// <summary>
        /// Indicates that Horizon has a valid connection to a server
        /// </summary>
        public bool Success;
    }
}
