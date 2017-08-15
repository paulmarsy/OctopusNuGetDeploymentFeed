using System;
using NuGet;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public class ClientCompatibility
    {
        /// <summary>
        ///     A set of client compatibilities with yielding the maximum set of packages.
        /// </summary>
        public static readonly ClientCompatibility Max = new ClientCompatibility(new SemanticVersion("2.0.0"));

        /// <summary>
        ///     A set of client compatibilities with yielding the minimum set of packages.
        /// </summary>
        public static readonly ClientCompatibility Default = new ClientCompatibility(new SemanticVersion("1.0.0"));

        public ClientCompatibility(SemanticVersion semVerLevel)
        {
            if (semVerLevel == null)
                throw new ArgumentNullException(nameof(semVerLevel));

            SemVerLevel = semVerLevel;
            AllowSemVer2 = semVerLevel.Version.Major >= 2;
        }

        public SemanticVersion SemVerLevel { get; }

        public bool AllowSemVer2 { get; }
    }
}