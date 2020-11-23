using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using horizon.Transport;

namespace horizon.Packets
{
    class DisconnectPacket : IPacket
    {
        public PacketType PacketId => PacketType.DisconnectPacket;
        public DisconnectReason Reason;
        public ValueTask SendPacket(BinaryAdapter adapter)
        {
            return adapter.WriteInt((int)Reason, false);
        }
    }
}
