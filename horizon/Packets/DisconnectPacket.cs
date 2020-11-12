using System;
using System.Collections.Generic;
using System.Text;
using horizon.Transport;

namespace horizon.Packets
{
    class DisconnectPacket : IPacket
    {
        public PacketType PacketId => PacketType.DisconnectPacket;
        public DisconnectReason Reason;
        public void SendPacket(BinaryAdapter adapter)
        {
            adapter.WriteInt((int)Reason);
        }
    }
}
