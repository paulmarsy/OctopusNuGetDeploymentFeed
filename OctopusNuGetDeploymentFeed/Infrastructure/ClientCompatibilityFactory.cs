using NuGet;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public static class ClientCompatibilityFactory
    {
        public static ClientCompatibility FromProperties(string unparsedSemVerLevel)
        {
            SemanticVersion semVerLevel;
            if (string.IsNullOrWhiteSpace(unparsedSemVerLevel) ||
                !SemanticVersion.TryParse(unparsedSemVerLevel, out semVerLevel))
                semVerLevel = ClientCompatibility.Default.SemVerLevel;

            if (semVerLevel == ClientCompatibility.Default.SemVerLevel)
                return ClientCompatibility.Default;
            if (semVerLevel == ClientCompatibility.Max.SemVerLevel)
                return ClientCompatibility.Max;

            return new ClientCompatibility(semVerLevel);
        }
    }
}