using Octopus.Server.Extensibility.Extensions;

namespace OctopusDeployNuGetFeed.Extension.Registration
{
    public interface IOctopusExtensionApiKeyService
    {
        string GenerateApiKey(IOctopusExtensionMetadata extension, string userId, params string[] requiredRoles);
    }
}