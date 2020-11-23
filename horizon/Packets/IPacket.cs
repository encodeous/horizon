using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using horizon.Transport;

namespace horizon.Packets
{
    interface IPacket
    {
        PacketType PacketId { get; }
        ValueTask SendPacket(BinaryAdapter adapter);
    }
}
