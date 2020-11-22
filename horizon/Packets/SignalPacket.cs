using System;
using System.Collections.Generic;
using System.Text;
using horizon.Transport;

namespace horizon.Packets
{
    /// <summary>
    /// A generic packet that represents a signal
    /// </summary>
    class SignalPacket : IPacket
    {
        public PacketType PacketId { get; }
        public int? Value { get; }
        public SignalPacket(PacketType id, int? value = null)
        {
            PacketId = id;
            Value = value;
        }
        public void SendPacket(BinaryAdapter adapter)
        {
            if(Value.HasValue) adapter.WriteInt(Value.Value);
            adapter.Flush();
        }
    }
}
