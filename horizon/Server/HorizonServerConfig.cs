using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace horizon.Server
{
    public class HorizonServerConfig
    {
        /// <summary>
        /// Users and Tokens list, initialized by default with user "default" and all permissions
        /// </summary>
        public Dictionary<string, HorizonUser> Users = new Dictionary<string, HorizonUser>(new KeyValuePair<string, HorizonUser>[1]
            {new KeyValuePair<string, HorizonUser>("default", new HorizonUser(){Whitelist = false})});
        /// <summary>
        /// Should the server allow Reverse Proxy Requests
        /// </summary>
        public bool AllowReverseProxy = false;
        /// <summary>
        /// Should the server allow Tunneling Requests
        /// </summary>
        public bool AllowTunnel = false;

        /// <summary>
        /// Horizon's local Port and IP Address
        /// </summary>
        public IPEndPoint Bind = new IPEndPoint(IPAddress.Any, 5050);
    }
}
