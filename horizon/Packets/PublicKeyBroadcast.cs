using System;
using System.Collections.Generic;
using System.Text;
using horizon.Transport;

namespace horizon.Packets
{
    /// <summary>
    /// Broadcasts the public key to the remote party
    /// </summary>
    struct PublicKeyBroadcast : IPacket
    {
        public PacketType PacketId => PacketType.PublicKeyBroadcast;
        public byte[] PublicKey;
        public void SendPacket(BinaryAdapter adapter)
        {
            adapter.WriteByteArray(PublicKey);
        }
    }
}
