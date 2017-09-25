﻿using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NuGet;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;

namespace OctopusDeployNuGetFeed
{
    public static class ExtensionMethods
    {
        internal static ILogger Logger { get; set; }

        public static bool WildcardMatch(this string input, string pattern)
        {
            return Regex.IsMatch(input, "^" + Regex.Escape(pattern).Replace("\\?", ".").Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
        }

        public static SemanticVersion ToSemanticVersion(this string version, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            if (!SemanticVersion.TryParse(version, out var semver))
                Logger?.Warning($"{callerTypeName}.{callerMemberName} Unable to convert to Semantic Version from: {version}");

            return semver;
        }

        public static IEnumerable<T> AsEnumerable<T>(this T value)
        {
            return new[] {value};
        }
    }
}