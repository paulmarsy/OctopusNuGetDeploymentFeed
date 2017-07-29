// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System.Web.Http;
using NuGet.Server.Core.Infrastructure;
using NuGet.Server.Core.Logging;
using NuGet.Server.V2.Controllers;
using NuGet.Server.V2.Samples.OwinHost.Octopus;

namespace NuGet.Server.V2.Samples.OwinHost.Controllers
{
    /// <summary>
    /// Controller that exposes Program.NuGetPublicRepository as NuGet OData feed
    /// that allows unauthenticated read/download access.
    /// Delete/upload requires user supplied ApiKey to match Program.ApiKey.
    /// </summary>
    [Authorize]
    public class OctopusDeployNuGetODataController : NuGetODataController
    {
        public OctopusDeployNuGetODataController()
            : base(new ConsoleLogger(),  Program.OctopusProjectPackageRepositoryFactory)
        {

        }
    }
}
