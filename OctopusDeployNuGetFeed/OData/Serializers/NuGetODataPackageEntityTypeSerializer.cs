using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.OData;
using System.Web.Http.OData.Extensions;
using System.Web.Http.OData.Formatter.Serialization;
using System.Web.Http.OData.Routing;
using Microsoft.Data.OData;
using Microsoft.Data.OData.Atom;
using NuGet;
using OctopusDeployNuGetFeed.Model;

namespace OctopusDeployNuGetFeed.OData.Serializers
{
    public class NuGetODataPackageEntityTypeSerializer : ODataEntityTypeSerializer
    {
        private const string DownloadContentType = "application/zip";

        public NuGetODataPackageEntityTypeSerializer(ODataSerializerProvider serializerProvider) : base(serializerProvider)
        {
        }

        public override ODataEntry CreateEntry(SelectExpandNode selectExpandNode, EntityInstanceContext entityInstanceContext)
        {
            var entry = base.CreateEntry(selectExpandNode, entityInstanceContext);

            var package = entityInstanceContext.EntityInstance as ODataPackage;
            if (package == null)
                return entry;

            // Set Atom entry metadata
            entry.SetAnnotation(new AtomEntryMetadata
            {
                Title = package.Id,
                Authors = new[] {new AtomPersonMetadata {Name = package.Authors}},
                Published = package.Published,
                Summary = package.Summary
            });

            // Set the ID and links. We have to do this because the self link should have a version containing
            // SemVer 2.0.0 metadata (e.g. 1.0.0+git).
            entry.Id = BuildId(package, entityInstanceContext);
            entry.ReadLink = new Uri(entry.Id);
            entry.EditLink = new Uri(entry.Id);

            // Add package download link
            entry.MediaResource = new ODataStreamReferenceValue
            {
                ContentType = DownloadContentType,
                ReadLink = BuildLinkForStreamProperty(package, entityInstanceContext)
            };

            // Make the download action target match the media resource link.
            entry.Actions = entry.Actions.Select(action => string.Equals("Download", action.Title, StringComparison.OrdinalIgnoreCase)
                ? new ODataAction
                {
                    Metadata = action.Metadata,
                    Target = entry.MediaResource.ReadLink,
                    Title = action.Title
                }
                : action).ToList();

            return entry;
        }

        private static string BuildId(ODataPackage package, EntityInstanceContext context)
        {
            var pathSegments = GetPackagePathSegments(package);
            return context.Url.CreateODataLink(pathSegments);
        }

        private static Uri BuildLinkForStreamProperty(ODataPackage package, EntityInstanceContext context)
        {
            var segments = GetPackagePathSegments(package);
            segments.Add(new ActionPathSegment("Download"));
            var downloadUrl = context.Url.CreateODataLink(segments);
            return new Uri(downloadUrl);
        }

        private static IList<ODataPathSegment> GetPackagePathSegments(ODataPackage package)
        {
            return new List<ODataPathSegment>
            {
                new EntitySetPathSegment("Packages"),
                new KeyValuePathSegment($"Id='{package.Id}',Version='{SemanticVersion.Parse(package.Version).ToNormalizedString()}'")
            };
        }
    }
}