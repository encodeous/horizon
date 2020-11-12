using System;
using System.Collections.Generic;
using System.Text;

namespace horizon.Legacy
{
    public static class Logger
    {
        public static LoggingLevel Level = LoggingLevel.Info;
        public enum LoggingLevel
        {
            Severe,
            Info,
            Verbose,
            Debug
        }
        public static void Log(this string s, LoggingLevel level)
        {
            if (level <= Level)
            {
                if (level == LoggingLevel.Info)
                {
                    $"[INFO {DateTime.Now:h:mm:ss tt}]: {s}".LogColor(ConsoleColor.White);
                }
                else if(level == LoggingLevel.Debug)
                {
                    $"[DEBUG {DateTime.Now:h:mm:ss tt}]: {s}".LogColor(ConsoleColor.Gray);
                }
                else if(level == LoggingLevel.Severe)
                {
                    $"[SEVERE {DateTime.Now:h:mm:ss tt}]: {s}".LogColor(ConsoleColor.Red);
                }
                else if(level == LoggingLevel.Verbose)
                {
                    $"[VERBOSE {DateTime.Now:h:mm:ss tt}]: {s}".LogColor(ConsoleColor.DarkYellow);
                }
            }
        }

        private static readonly object WriteLock = new object();

        public static void LogColor(this string text, ConsoleColor color)
        {
            lock (WriteLock)
            {
                ConsoleColor oc = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ForegroundColor = oc;
            }
        }
    }
}
