using System.Text;
using System.Text.RegularExpressions;

namespace NuGet.Server.V2.Samples.OwinHost.Octopus
{
    public static class StringExtensions
    {
        public static bool WildcardMatch(this string input, string pattern)
        {
            return Regex.IsMatch(input, $"^{Regex.Escape(pattern).Replace("\\?", ".").Replace("\\*", ".*")}$", RegexOptions.IgnoreCase);
        }

    }
}
