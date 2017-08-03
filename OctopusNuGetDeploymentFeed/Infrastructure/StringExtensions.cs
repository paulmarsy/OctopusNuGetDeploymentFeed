using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NuGet;
using OctopusDeployNuGetFeed.DataServices;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public static class StringExtensions
    {
        public static bool WildcardMatch(this string input, string pattern)
        {
            return Regex.IsMatch(input, $"^{Regex.Escape(pattern).Replace("\\?", ".").Replace("\\*", ".*")}$", RegexOptions.IgnoreCase);
        }
        public static string GetHash(this string input, string hashAlgorithm)
        {
            return input.GetHash(new CryptoHashProvider(hashAlgorithm));
        }

        private static string GetHash(this string input, IHashProvider hashProvider)
        {
            return Convert.ToBase64String(hashProvider.CalculateHash(Encoding.UTF8.GetBytes(input)));
        }
        public static string GetHash(this Stream stream, string hashAlgorithm)
        {
            return stream.GetHash(new CryptoHashProvider(hashAlgorithm));
        }

        private static string GetHash(this Stream stream, IHashProvider hashProvider)
        {
                return Convert.ToBase64String(hashProvider.CalculateHash(stream));
        }
    }
}