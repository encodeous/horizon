using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace horizon.Server
{
    class ServerConnectionResponse
    {
        public bool Accepted { get; set; }
        public byte[] HashedBytes { get; set; }
        public string DisconnectMessage { get; set; }
        
    }
}
