using System;
using System.Runtime.Serialization;

namespace OctopusDeployNuGetFeed.Model
{
    [DataContract]
    public class ODataPackage
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string Version { get; set; }

        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public string Summary { get; set; }

        [DataMember]
        public string ReleaseNotes { get; set; }

        [DataMember]
        public DateTimeOffset? Published { get; set; }

        [DataMember]
        public string Authors { get; set; }

        [DataMember]
        public bool IsAbsoluteLatestVersion { get; set; }

        [DataMember]
        public bool IsLatestVersion { get; set; }

        [DataMember]
        public bool Listed { get; set; }

        public static implicit operator ODataPackage(SearchPackage package) => FromNuGetPackage(package);
        public static implicit operator ODataPackage(ProjectPackage package) => FromNuGetPackage(package);
        public static implicit operator ODataPackage(ReleasePackage package) => FromNuGetPackage(package);
        public static ODataPackage FromNuGetPackage(INuGetPackage package)
        {
            if (package == null)
                return null;
            return new ODataPackage
            {
                Version = package.Version,
                Id = package.Id,
                Summary = package.Summary,
                Description = package.Description,
                Authors = package.Authors,
                IsAbsoluteLatestVersion = package.IsAbsoluteLatestVersion,
                IsLatestVersion = package.IsLatestVersion,
                ReleaseNotes = package.ReleaseNotes,
                Published = package.Published,
                Title = package.Title,
                Listed = package.Listed
            };
        }
    }
}