using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace horizon.Legacy
{
    public class HorizonRequest
    {
        public int RequestedPort;

        public string RequestedHost;

        public string UserId;

        /// <summary>
        /// User Token Hash with Salt, and RequestTime Hashed in with it
        /// If the request time is from over 1 minute ago, deny access to the client.
        /// </summary>
        public byte[] UserTokenHash;

        /// <summary>
        /// Salt should be randomly generated to be secure
        /// </summary>
        public byte[] Salt;

        /// <summary>
        /// The time the request was generated
        /// </summary>
        public DateTime RequestTime;

        /// <summary>
        /// Identifies as true if the packet is a ping request
        /// </summary>
        public bool PingPacket;
    }
}
