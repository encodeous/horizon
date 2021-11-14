using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace horizon_cli
{
    class Extensions
    {
        public static string GetScheme(string uri)
        {
            Regex reg = new Regex("^(([^:/?#]+):)?(//([^/?#]*))?([^?#]*)(\\?([^#]*))?(#(.*))?");
            return reg.Match(uri).Groups[2].Captures[0].Value;
        }

        public static (int, string, int)? ParseMap(string str)
        {
            if(str.Count(x => x == ':') < 2)
                return null;
            
            var id1 = str.IndexOf(":", StringComparison.Ordinal);
            var id2 = str.LastIndexOf(":", StringComparison.Ordinal);
            string s1 = str[..id1], s2 = str[(id1+1)..id2], s3 = str[(id2+1)..];
            if (string.IsNullOrEmpty(s2))
            {
                return null;
            }
            if(!int.TryParse(s1, out var port) || port is <= 0 or > 65535)
            {
                return null;
            }
            if(!int.TryParse(s3, out var port2) || port2 is <= 0 or > 65535)
            {
                return null;
            }

            return (port, s2, port2);
        }
    }
}
