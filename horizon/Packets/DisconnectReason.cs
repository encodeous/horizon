using System;
using System.Collections.Generic;
using System.Text;

namespace horizon.Packets
{
    /// <summary>
    /// Reason why a conduit was disconnected
    /// </summary>
    public enum DisconnectReason
    {
        /// <summary>
        /// Heartbeat timeout
        /// </summary>
        Timeout,
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
