using Octopus.Client.Model;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class EnhancedNuGetFeedResource : NuGetFeedResource
    {
        public EnhancedNuGetFeedResource(FeedResource feed)
        {
            if (feed != null)
            {
                Id = feed.Id;
                Name = feed.Name;
                FeedUri = feed.FeedUri;
                Username = feed.Username;
                Password = feed.Password;
                Links = feed.Links;
            }
        }

        [Writeable]
        public bool EnhancedMode { get; set; }
    }
}