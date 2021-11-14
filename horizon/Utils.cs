using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;

namespace horizon
{
    public class Utils
    {
        public static async Task<IPAddress> ResolveDns(string addr)
        {
            if (IPAddress.TryParse(addr, out var k)) return k;
            try
            {
                var lookup = new LookupClient(IPAddress.Parse(HorizonStaticOptions.DnsServer));
                if ((await lookup.QueryAsync(addr, QueryType.A))
                    .Answers.OfRecordType(ResourceRecordType.A)
                    .FirstOrDefault() is ARecord query1) return query1.Address;
                
                if ((await lookup.QueryAsync(addr, QueryType.AAAA))
                    .Answers.OfRecordType(ResourceRecordType.AAAA)
                    .FirstOrDefault() is AaaaRecord query2) return query2.Address;
            }
            catch
            {
                
            }
            return null;
        }
        public static bool IsValidRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;

            try
            {
                Regex.Match("", pattern);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }
    }
}