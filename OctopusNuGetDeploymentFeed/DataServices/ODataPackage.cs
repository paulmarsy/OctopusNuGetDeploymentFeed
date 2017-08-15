using System;

namespace OctopusDeployNuGetFeed.DataServices
{
    public class ODataPackage
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public string ReleaseNotes { get; set; }

        public DateTimeOffset? Published { get; set; }
        public string Authors { get; set; }

        public bool IsAbsoluteLatestVersion { get; set; }

        public bool IsLatestVersion { get; set; }

        public bool Listed { get; set; }
    }
}