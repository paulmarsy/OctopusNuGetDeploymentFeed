using System;
using System.Runtime.CompilerServices;

namespace OctopusDeployNuGetFeed.Logging
{
    public class ConsoleLogger : ILogger
    {
        public static bool IsInteractive = Environment.UserInteractive;

        public void Critical(string message)
        {
            WriteLine(ConsoleColor.DarkRed, ConsoleColor.Red, "CRITICAL", message);
        }

        public void Error(string message)
        {
            WriteLine(ConsoleColor.DarkRed, ConsoleColor.Red, "ERROR", message);
        }

        public void Warning(string message)
        {
            WriteLine(ConsoleColor.DarkYellow, ConsoleColor.Yellow, "WARNING", message);
        }

        public void Verbose(string message)
        {
            if (IsInteractive)
                WriteLine(message);
        }

        public void Info(string message)
        {
            WriteLine(message);
        }

        public void Exception(Exception exception, string callerFilePath = null, string callerMemberName = null)
        {
        }

        public void UnhandledException(Exception exception)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteLine(string message)
        {
            if (IsInteractive)
                Console.WriteLine(message);
            else
                Console.Out.WriteLineAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteLine(ConsoleColor prefixColour, ConsoleColor colour, string prefix, string message)
        {
            if (IsInteractive)
            {
                Console.BackgroundColor = prefixColour;
                Console.ForegroundColor = GetContrastingForegroundColor(prefixColour);
                Console.Write($" {prefix} ");
                Console.ResetColor();

                Console.ForegroundColor = colour;
                Console.WriteLine(" " + message);
                Console.ResetColor();
            }
            else
            {
                Console.Error.WriteLineAsync($"{prefix} {message}");
            }
        }

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