using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace horizon
{
    public class UserPermission
    {
        /// <summary>
        /// The Unique Identifier for the user
        /// </summary>
        public string UserId;
        /// <summary>
        /// A shared secret key used to connect to the server
        /// </summary>
        public string UserToken;

        /// <summary>
        /// A list of allowed hosts to tunnel to
        /// </summary>
        public List<string> AllowedRemoteServers = new List<string>();
        /// <summary>
        /// A list of allowed ports to tunnel to
        /// </summary>
        public List<int> AllowedRemotePorts = new List<int>();
        /// <summary>
        /// A list of blocked hosts
        /// </summary>
        public List<string> DisallowedRemoteServers = new List<string>();
        /// <summary>
        /// A list of blocked ports
        /// </summary>
        public List<int> DisallowedRemotePorts = new List<int>();
        /// <summary>
        /// Allows the user to connect to ANY remote server on allowed ports
        /// </summary>
        public bool AllowAnyServer;
        /// <summary>
        /// Allows the user to connect to ANY remote port on allowed servers
        /// </summary>
        public bool AllowAnyPort;

        /// <summary>
        /// Gives the user all permissions and bypass permissions to all blacklists and whitelists
        /// </summary>
        public bool Administrator;
    }
}
