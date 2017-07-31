// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

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
    internal class NuGetODataControllerConfigurationAttribute : Attribute, IControllerConfiguration
    {
        private static IList<ODataMediaTypeFormatter> _formatters;
        private static readonly object _syncLock = new object();


        public void Initialize(HttpControllerSettings controllerSettings, HttpControllerDescriptor controllerDescriptor)
        {
            controllerSettings.Formatters.Clear();
            controllerSettings.Formatters.InsertRange(0, GetFormatters());
        }

        private IList<ODataMediaTypeFormatter> GetFormatters()
        {
            if (_formatters == null)
                lock (_syncLock)
                {
                    if (_formatters == null)
                    {
                        var serProvider = new CustomSerializerProvider(provider => new NuGetEntityTypeSerializer(provider));
                        var createdFormatters = ODataMediaTypeFormatters.Create(serProvider, new DefaultODataDeserializerProvider());

                        var jsonFormatters = createdFormatters.Where(x => x.SupportedMediaTypes.Any(y => y.MediaType.Contains("json")));
                        createdFormatters.RemoveAll(x => jsonFormatters.Contains(x));
                        var xmlFormatterIndex = createdFormatters.IndexOf(createdFormatters.Last(x => x.SupportedMediaTypes.Any(y => y.MediaType.Contains("xml"))));
                        foreach (var formatter in jsonFormatters)
                            createdFormatters.Insert(xmlFormatterIndex++, formatter);
                        _formatters = createdFormatters;
                    }
                }
            return _formatters;
        }
    }
}