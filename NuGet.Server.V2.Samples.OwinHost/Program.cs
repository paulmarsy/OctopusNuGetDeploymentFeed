// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 
using System;
using System.Collections.Generic;
using Microsoft.Owin.Hosting;
using NuGet.Server.Core.Infrastructure;
using NuGet.Server.Core.Logging;
using NuGet.Server.V2.Samples.OwinHost.Octopus;

namespace NuGet.Server.V2.Samples.OwinHost
{
    class Program
    {
        public static IServerPackageRepositoryFactory OctopusProjectPackageRepositoryFactory { get; private set; }

        public const string ApiKey = "key123"; 

        static void Main(string[] args)
        {
            var baseAddress = "http://localhost:9000/";

            // Set up a common settingsProvider to be used by all repositories. 
            // If a setting is not present in dictionary default value will be used.
            var settings = new Dictionary<string, bool>();
            settings.Add("enableDelisting", false);                         //default=false
            settings.Add("enableFrameworkFiltering", false);                //default=false
            settings.Add("ignoreSymbolsPackages", true);                    //default=false
            settings.Add("allowOverrideExistingPackageOnPush", true);       //default=true
            var settingsProvider = new DictionarySettingsProvider(settings);

            var logger = new ConsoleLogger();

            //Sets up three repositories with seperate packages in each feed. These repositories are used by our controllers.
            //In a real world application the repositories will probably be inserted through DI framework, or created in the controllers constructor.
            OctopusProjectPackageRepositoryFactory = new OctopusProjectPackageRepositoryFactory(logger);

            // Start OWIN host, which in turn will create a new instance of Startup class, and execute its Configuration method.
            using (WebApp.Start<Startup>(url: baseAddress))
            {
                Console.WriteLine("Server listening at baseaddress: " + baseAddress);
                Console.WriteLine("[ENTER] to close server");
                Console.ReadLine();
            }
        }
    }
}
