using System;
using System.Collections.Generic;
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
    }
}
