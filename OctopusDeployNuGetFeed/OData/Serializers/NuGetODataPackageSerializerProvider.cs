using System.Web.Http.OData.Formatter.Serialization;
using Microsoft.Data.Edm;

namespace OctopusDeployNuGetFeed.OData.Serializers
{
    public class NuGetODataPackageSerializerProvider : DefaultODataSerializerProvider
    {
        private readonly ODataEdmTypeSerializer _entitySerializer;

        public NuGetODataPackageSerializerProvider()
        {
            _entitySerializer = new NuGetODataPackageEntityTypeSerializer(this);
        }

        public override ODataEdmTypeSerializer GetEdmTypeSerializer(IEdmTypeReference edmType)
        {
            return edmType.IsEntity() ? _entitySerializer : base.GetEdmTypeSerializer(edmType);
        }
    }
}