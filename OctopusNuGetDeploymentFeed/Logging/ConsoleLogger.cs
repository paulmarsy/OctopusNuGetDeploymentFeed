using System;
using System.Runtime.CompilerServices;

namespace OctopusDeployNuGetFeed.Logging
{
    public class ConsoleLogger : ILogger
    {
        public void Critical(string message)
        {
            WritePrefixLine(ConsoleColor.DarkRed, ConsoleColor.Red, "CRITICAL", message);
        }

        public void Error(string message)
        {
            WritePrefixLine(ConsoleColor.DarkRed, ConsoleColor.Red, "ERROR", message);
        }

        public void Warning(string message)
        {
            WritePrefixLine(ConsoleColor.DarkYellow, ConsoleColor.Yellow, "WARNING", message);
        }

        public void Info(string message)
        {
            Console.WriteLine(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WritePrefixLine(ConsoleColor prefixColour, ConsoleColor colour, string prefix, string message)
        {
            Console.BackgroundColor = prefixColour;
            Console.ForegroundColor = GetContrastingForegroundColor(prefixColour);
            Console.Write($" {prefix} ");
            Console.ResetColor();

            Console.ForegroundColor = colour;
            Console.WriteLine(" " + message);
            Console.ResetColor();
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