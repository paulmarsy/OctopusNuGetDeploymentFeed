using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OctopusDeployNuGetFeed.Logging
{
    public class ConsoleLogger : ILogger
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Critical(string message)
        {
            WriteLineErr("CRITICAL ", message);
            WriteLineColored(ConsoleColor.DarkRed, ConsoleColor.Red, "CRITICAL", message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Error(string message)
        {
            WriteLineErr("ERROR ", message);
            WriteLineColored(ConsoleColor.DarkRed, ConsoleColor.Red, "ERROR", message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Warning(string message)
        {
            WriteLineErr("WARNING ", message);
            WriteLineColored(ConsoleColor.DarkYellow, ConsoleColor.Yellow, "WARNING", message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Info(string message)
        {
            WriteLineStd(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exception(Exception exception, string callerFilePath = null, string callerMemberName = null)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnhandledException(Exception exception)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteLineStd(string message)
        {
            Console.Out.WriteLineAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("RELEASE")]
        private static void WriteLineErr(string prefix, string message)
        {
            Console.Error.WriteLineAsync(prefix + message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("DEBUG")]
        private static void WriteLineColored(ConsoleColor prefixColour, ConsoleColor colour, string prefix, string message)
        {
            Console.BackgroundColor = prefixColour;
            Console.ForegroundColor = GetContrastingForegroundColor(prefixColour);
            Console.Write($" {prefix} ");
            Console.ResetColor();

            Console.ForegroundColor = colour;
            Console.WriteLine(" " + message);
            Console.ResetColor();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ConsoleColor GetContrastingForegroundColor(ConsoleColor backgroundColor)
        {
            switch (backgroundColor)
            {
                case ConsoleColor.White:
                    return ConsoleColor.Black;
                case ConsoleColor.Cyan:
                    return ConsoleColor.Black;
                case ConsoleColor.DarkYellow:
                    return ConsoleColor.Black;
                case ConsoleColor.Yellow:
                    return ConsoleColor.Black;
                case ConsoleColor.Gray:
                    return ConsoleColor.Black;
                case ConsoleColor.Green:
                    return ConsoleColor.Black;
                default:
                    return ConsoleColor.White;
            }
        }
    }
}