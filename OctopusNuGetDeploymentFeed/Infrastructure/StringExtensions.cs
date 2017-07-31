using System.Text.RegularExpressions;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public static class StringExtensions
    {
        public static bool WildcardMatch(this string input, string pattern)
        {
            return Regex.IsMatch(input, $"^{Regex.Escape(pattern).Replace("\\?", ".").Replace("\\*", ".*")}$", RegexOptions.IgnoreCase);
        }
    }
}