using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using horizon;
using horizon.Server;

namespace horizon_cli
{
    class Storage
    {
        public static HorizonServerConfig GetServerConfig(string path)
        {
            return JsonSerializer.Deserialize<HorizonServerConfig>(File.ReadAllText(path));
        }
        public static void SaveServerConfig(string path, HorizonServerConfig config)
        {
            var opt = new JsonSerializerOptions {WriteIndented = true};
            var v = JsonSerializer.Serialize(config, opt);
            File.WriteAllText(path, v);
        }
    }
}
