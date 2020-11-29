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
                        PrintColor(ConsoleColor.Green, "Information");
                        break;
                    case LogLevel.None:
                        PrintColor(ConsoleColor.White, "Log");
                        break;
                    case LogLevel.Trace:
                        PrintColor(ConsoleColor.Cyan, "Trace");
                        break;
                    case LogLevel.Warning:
                        PrintColor(ConsoleColor.Yellow, "Critical");
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
