using System;
using System.Collections.Generic;
using System.Text;
using horizon.Transport;

namespace horizon.Packets
{
    interface IPacket
    {
        PacketType PacketId { get; }
        void SendPacket(BinaryAdapter adapter);
    }
}
