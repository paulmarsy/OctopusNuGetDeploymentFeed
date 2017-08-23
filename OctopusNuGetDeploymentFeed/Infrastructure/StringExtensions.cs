using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Autofac;
using NuGet;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public static class StringExtensions
    {
        public static bool WildcardMatch(this string input, string pattern)
        {
            return Regex.IsMatch(input, "^" + Regex.Escape(pattern).Replace("\\?", ".").Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
        }

        public static SemanticVersion ToSemanticVersion(this string version, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            if (!SemanticVersion.TryParse(version, out SemanticVersion semver))
                Program.Container.Resolve<ILogger>().Warning($"{callerTypeName}.{callerMemberName} Unable to convert to Semantic Version from: {version}");

            return semver;
        }
    }
}