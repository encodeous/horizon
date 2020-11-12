using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace horizon.Transport
{
    /// <summary>
    /// A class that handles a single TCP connection
    /// </summary>
    public class Fiber
    {
        /// <summary>
        /// Checks if the Fiber is still connected
        /// </summary>
        public bool Connected { get; internal set; }
        /// <summary>
        /// Underlying Datastream
        /// </summary>
        public NetworkStream Remote;
        public Fiber(NetworkStream remote)
        {
            Connected = false;
            Remote = remote;
        }
        /// <summary>
        /// Disconnect the fiber
        /// </summary>
        public void Disconnect()
        {
            Remote.Dispose();
            Connected = false;
        }
    }
}
