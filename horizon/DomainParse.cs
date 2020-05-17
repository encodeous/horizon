using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace horizon
{
    class DomainParse
    {
        public static string GetDomain(string url)
        {
            Regex reg = new Regex("^(([^:/?#]+):)?(//([^/?#]*))?([^?#]*)(\\?([^#]*))?(#(.*))?");
            return reg.Match(url).Groups[4].Captures[0].Value;
        }
    }
}
