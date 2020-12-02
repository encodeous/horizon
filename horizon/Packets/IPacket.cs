using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using horizon.Transport;

namespace horizon.Packets
{
    /// <summary>
    /// Describes a Generic packet
    /// </summary>
    interface IPacket
    {
        /// <summary>
        /// Id of the packet
        /// </summary>
        PacketType PacketId { get; }
        /// <summary>
        /// A function that when called sends the packet's stored information, DO NOT LOCK THE ADAPTER ELSE A DEADLOCK WILL OCCUR!
        /// </summary>
        /// <param name="adapter"></param>
        /// <returns></returns>
        ValueTask SendPacket(BinaryAdapter adapter);
    }
}
