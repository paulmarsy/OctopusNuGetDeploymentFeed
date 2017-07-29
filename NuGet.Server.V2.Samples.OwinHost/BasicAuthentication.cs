// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Octopus.Client;

namespace NuGet.Server.V2.Samples.OwinHost
{
    public class BasicAuthentication : OwinMiddleware
    {
        public BasicAuthentication(OwinMiddleware next) :
            base(next)
        {

        }

        public override async Task Invoke(IOwinContext context)
        {
            var response = context.Response;
            var request = context.Request;

            response.OnSendingHeaders(state =>
            {
                var owinResponse = (OwinResponse)state;

                if (owinResponse.StatusCode == 401)
                {
                    owinResponse.Headers.Add("WWW-Authenticate", new[] { "Basic" });
                }
            }, response);

            var header = request.Headers["Authorization"];

            if (!String.IsNullOrWhiteSpace(header))
            {
                var authHeader = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(header);

                if ("Basic".Equals(authHeader.Scheme, StringComparison.OrdinalIgnoreCase))
                {
                    var parameter = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter));

                    var lastColonIndex = parameter.LastIndexOf(':');
                    if (lastColonIndex != -1)
                    {
                        var username = parameter.Substring(0, lastColonIndex).Trim();
                        var password = parameter.Substring(lastColonIndex + 1).Trim();

                        if (ValidateUser(username, password))
                        {
                            SetClaimsIdentity(request, username, password);
                        }
                    }
                }
            }

            await Next.Invoke(context);
        }

        protected virtual void SetClaimsIdentity(IOwinRequest request, string username, string password)
        {
            var id = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Uri, username),
                new Claim(ClaimTypes.UserData, password)
            }, "Basic");

            request.User = new ClaimsPrincipal(id);
        }
        
        protected virtual bool ValidateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            return true;
        }
    }
}
