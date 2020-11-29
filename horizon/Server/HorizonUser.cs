using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace horizon.Server
{
    public class HorizonUser : IEqualityComparer<HorizonUser>
    {
        /// <summary>
        /// Client authentication token
        /// </summary>
        public string Token { get; set; }
        /// <summary>
        /// Specifies whether the permission mode is white-list match or blacklist match
        /// </summary>
        public bool Whitelist { get; set; } = false;
        /// <summary>
        /// A set of Hosts that a client is allowed/denied from connecting to
        /// </summary>
        public RemotePattern[] RemotesPattern { get; set; } = Array.Empty<RemotePattern>();
        /// <summary>
        /// A set of ports that a client is allowed/denied from binding to as their reverse proxy.
        /// </summary>
        public int[] ReverseBinds { get; set; } = Array.Empty<int>();

        public bool Equals(HorizonUser x, HorizonUser y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Token == y.Token && x.Whitelist == y.Whitelist && x.RemotesPattern.SequenceEqual(y.RemotesPattern) && x.ReverseBinds.SequenceEqual(y.ReverseBinds);
        }

        public int GetHashCode(HorizonUser obj)
        {
            return HashCode.Combine(obj.Token, obj.Whitelist, obj.RemotesPattern, obj.ReverseBinds);
        }
    }
}
