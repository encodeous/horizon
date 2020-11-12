using System;
using System.Collections.Generic;
using System.Text;

namespace horizon.Packets
{
    public enum DisconnectReason
    {
        /// <summary>
        /// Heartbeat timeout
        /// </summary>
        Timeout,
        /// <summary>
        /// Failed Connecting to Endpoint
        /// </summary>
        EndpointError,
        /// <summary>
        /// Graceful shutdown
        /// </summary>
        Shutdown,
        /// <summary>
        /// Forceful shutdown for unknown reason
        /// </summary>
        Terminated
    }
}
