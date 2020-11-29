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
        public HorizonUser[] Users { get; set; } =
        {
            new HorizonUser()
            {
                Whitelist = false,
                Token = "default"
            },
            new HorizonUser()
            {
                Whitelist = true,
                Token = "default-whitelist",
                RemotesPattern = new[]
                    {new RemotePattern() {HostRegex = "[\\s\\S]", PortRangeStart = 0, PortRangeEnd = 65353}},
                ReverseBinds = new[] {8080}
            }
        };

        /// <summary>
        /// Horizon's local Port and IP Address
        /// </summary>
        public string Bind { get; set; } = "0.0.0.0:5050";
    }
}
