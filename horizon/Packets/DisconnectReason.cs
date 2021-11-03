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
        Terminated,
        /// <summary>
        /// Usually an error or warning, a string displaying the disconnect reason
        /// </summary>
        Textual
    }
}
