using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Controllers;
using System.Web.Http.OData.Formatter;
using System.Web.Http.OData.Formatter.Deserialization;
using NuGet;
using OctopusDeployNuGetFeed.OData.Serializers;

namespace OctopusDeployNuGetFeed.OData
{
    [AttributeUsage(AttributeTargets.Class)]
    public class NuGetODataControllerAttribute : Attribute, IControllerConfiguration
    {
        public void Initialize(HttpControllerSettings controllerSettings, HttpControllerDescriptor controllerDescriptor)
        {
            controllerSettings.Formatters.Clear();
            controllerSettings.Formatters.InsertRange(0, GetFormatters());
        }

        private static IEnumerable<ODataMediaTypeFormatter> GetFormatters()
        {
            var formatters = ODataMediaTypeFormatters.Create(new NuGetODataPackageSerializerProvider(), new DefaultODataDeserializerProvider());

            var jsonFormatters = formatters.Where(x => x.SupportedMediaTypes.Any(y => y.MediaType.Contains("json")));
            formatters.RemoveAll(x => jsonFormatters.Contains(x));

            var xmlFormatterIndex = formatters.IndexOf(formatters.Last(x => x.SupportedMediaTypes.Any(y => y.MediaType.Contains("xml"))));

            foreach (var formatter in jsonFormatters)
                formatters.Insert(xmlFormatterIndex++, formatter);

            return formatters;
        }
    }
}