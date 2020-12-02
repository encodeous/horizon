using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace horizon
{
    public static class Logger
    {
        private static object lck = new object();
        public static LogLevel ApplicationLogLevel = LogLevel.Information;
        /// <summary>
        /// Horizon Logging Function, to set the Application's logging level see <see cref="ApplicationLogLevel"/>
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="level"></param>
        public static void Log(this string msg, LogLevel level)
        {
            if (level < ApplicationLogLevel) return;
            lock (lck)
            {
                LogPrefix();
                switch (level)
                {
                    case LogLevel.Critical:
                        PrintColor(ConsoleColor.Red, "Critical");
                        break;
                    case LogLevel.Debug:
                        PrintColor(ConsoleColor.Gray, "Debug");
                        break;
                    case LogLevel.Error:
                        PrintColor(ConsoleColor.Magenta, "Error");
                        break;
                    case LogLevel.Information:
                        PrintColor(ConsoleColor.Green, "Info");
                        break;
                    case LogLevel.None:
                        PrintColor(ConsoleColor.White, "Log");
                        break;
                    case LogLevel.Trace:
                        PrintColor(ConsoleColor.Cyan, "Trace");
                        break;
                    case LogLevel.Warning:
                        PrintColor(ConsoleColor.Yellow, "Warn");
                        break;
                }
                Console.WriteLine($"] {msg}");
            }
        }

        private static void LogPrefix()
        {
            Console.Write($"{DateTime.Now.ToString("MMM dd, yyyy h:mm:ss tt")}: [");
        }

        public static void PrintColor(ConsoleColor color, string value)
        {
            var c = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(value);
            Console.ForegroundColor = c;
        }
    }
}
