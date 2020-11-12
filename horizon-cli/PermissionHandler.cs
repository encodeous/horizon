using System;
using System.Collections.Generic;
using System.IO;
using horizon;
using horizon.Legacy;
using Newtonsoft.Json;

namespace horizon_cli
{
    class PermissionHandler
    {
        public static List<UserPermission> GetPermissionInfo(string path)
        {
            return JsonConvert.DeserializeObject<List<UserPermission>>(File.ReadAllText(path));
        }
        public static void SetPermissionInfo(string path, List<UserPermission> permission)
        {
            var v = JsonConvert.SerializeObject(permission);
            File.WriteAllText(path, v);
        }
    }
}
