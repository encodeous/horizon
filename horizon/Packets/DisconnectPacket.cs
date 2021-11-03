using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using horizon.Transport;

namespace horizon.Packets
{
    /// <summary>
    /// A packet wrapper handling the sending of disconnect events
    /// </summary>
    class DisconnectPacket : IPacket
    {
        public PacketType PacketId => PacketType.DisconnectPacket;
        public DisconnectReason Reason;
        public string StringReason;
        public async ValueTask SendPacket(BinaryAdapter adapter)
        {
            if (Reason == DisconnectReason.Textual)
            {
                await adapter.WriteInt((int)Reason, false);
                await adapter.WriteString(StringReason);
            }
            await adapter.WriteInt((int)Reason, false);
        }
    }
}
