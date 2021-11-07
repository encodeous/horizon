using System;
using System.Collections.Generic;
using System.Text;

namespace horizon.Packets
{
    /// <summary>
    /// Types of different packets in Horizon
    /// </summary>
    public enum PacketType
    {
        /// <summary>
        /// Heartbeat packet
        /// </summary>
        Heartbeat,
        /// <summary>
        /// Input node signal to the output node to add a fiber
        /// </summary>
        AddFiber,
        /// <summary>
        /// A signal from the output node that a fiber was created successfully
        /// </summary>
        FiberAdded,
        /// <summary>
        /// A signal from the output node that a fiber cannot be created
        /// </summary>
        FiberNotAdded,
        /// <summary>
        /// A signal from a node that a fiber is no longer accepting data
        /// </summary>
        FiberNotAccepting,
        /// <summary>
        /// Disconnection Packet
        /// </summary>
        DisconnectPacket
    }
}
