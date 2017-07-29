// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;

namespace NuGet.Server.Core.Infrastructure
{
    public static class JsonNetExtensions
    {
        private static readonly JsonSerializer JsonSerializer = new JsonSerializer
        { 
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.Indented
        };
        public static void SerializeInto(this object @object, Stream stream)
        {
            using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, 1024, true))
            using (var jsonTextWriter = new JsonTextWriter(streamWriter))
            {
                JsonSerializer.Serialize(jsonTextWriter, @object);
            }
        }
    }
}