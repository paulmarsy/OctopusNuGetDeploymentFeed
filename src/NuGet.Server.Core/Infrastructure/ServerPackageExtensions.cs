// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Server.Core.Infrastructure
{
    public static class ServerPackageExtensions
    {
        public static bool IsReleaseVersion(this IServerPackage package)
        {
            return string.IsNullOrEmpty(package.Version.SpecialVersion);
        }

   
 
        
    }
}
