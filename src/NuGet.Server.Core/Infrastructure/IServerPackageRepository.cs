// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Server.Core.Infrastructure
{
    public interface IServerPackageRepositoryFactory
    {
        IServerPackageRepository GetPackageRepository(IPrincipal user);
    }
    public interface IServerPackageRepository : IDisposable
    {
        Task<IEnumerable<IServerPackage>> GetPackagesAsync(string id, string version, CancellationToken token);

        Task<IEnumerable<IServerPackage>> SearchAsync(
            string searchTerm,
            bool allowPrereleaseVersions,
            CancellationToken token);


        bool IsAuthenticated { get; }
        string BaseUri { get; }
        string ApiKey { get; }
    }
}
