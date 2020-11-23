using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx.Synchronous;

namespace horizon.Utilities
{
    class TaskEx
    {
        public static void RunBgLong(Func<Task> fn)
        {
            Task.Run(fn);
        }
    }
}
