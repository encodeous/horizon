using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace horizon.Threading
{
    /// <summary>
    /// A class that handles and dispatches events/tasks
    /// </summary>
    public class Dispatcher
    {
        private Channel<Func<ValueTask>> DLive = Channel.CreateUnbounded<Func<ValueTask>>();
        private Channel<Func<ValueTask>> DNormal = Channel.CreateUnbounded<Func<ValueTask>>();
        private Channel<Func<ValueTask>> DSlow = Channel.CreateUnbounded<Func<ValueTask>>();
        private bool isRunning = false;
        public enum Priority
        {
            Live, // Performs live-on-the-wire events like packet dispatch
            Normal, // Performs normal events
            Slow // Performs slow events like creating sockets
        }
        public void Start()
        {
            isRunning = true;
            Task.Factory.StartNew(() => AsyncDispatcher(DLive), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => AsyncDispatcher(DNormal), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => AsyncDispatcher(DSlow), TaskCreationOptions.LongRunning);
        }

        public void Add(Func<ValueTask> op, Priority dPriority = Priority.Normal)
        {
            switch (dPriority)
            {
                case Priority.Live:
                    DLive.Writer.WriteAsync(op).GetAwaiter().GetResult();
                    break;
                case Priority.Normal:
                    DNormal.Writer.WriteAsync(op).GetAwaiter().GetResult();
                    break;
                case Priority.Slow:
                    DSlow.Writer.WriteAsync(op).GetAwaiter().GetResult();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dPriority), dPriority, null);
            }
        }

        public void Stop()
        {
            isRunning = false;
        }

        private async Task AsyncDispatcher(Channel<Func<ValueTask>> dSource)
        {
            while (isRunning)
            {
                var val = await dSource.Reader.WaitToReadAsync();
                if (!val) break;
                try
                {
                    await (await dSource.Reader.ReadAsync()).Invoke();
                }
                catch(Exception e)
                {
                    $"Error occurred while dispatching event: {e.Message} {e.StackTrace}".Log(LogLevel.Trace);
                }
            }
        }
    }
}