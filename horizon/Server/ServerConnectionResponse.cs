using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace horizon.Server
{
    /// <summary>
    /// An internal class storing the server response message used in the connection handshake
    /// </summary>
    class ServerConnectionResponse
    {
        public bool Accepted { get; set; }
        public string DisconnectMessage { get; set; }
        
    }
}
