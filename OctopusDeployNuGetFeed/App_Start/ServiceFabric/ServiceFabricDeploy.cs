using System;
using System.Fabric;
using System.Fabric.Description;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.ServiceFabric
{
    public class ServiceFabricDeploy : IProgram
    {
        private const string ImageStoreServiceConnectionString = "fabric:ImageStore";
        private const int ClusterManagementPort = 19000;
        private const string ApplicationTypeName = nameof(OctopusDeployNuGetFeed);
        private const string ApplicationManifestName = "ApplicationManifest.xml";
        private const string ServiceManifestName = "ServiceManifest.xml";
        private const string SettingsName = "Settings.xml";
        private readonly ILogger _logger;

        public ServiceFabricDeploy(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<int> Main(string[] args)
        {
            var packagePath = CreateApplicationPackage();

            var clusterFqdn = args[1];
            var cerThumbprint = args[2];
            _logger.Info($"Connecting to service fabric cluster {clusterFqdn} with certificate {cerThumbprint}...");
            var xc = GetCredentials(cerThumbprint, cerThumbprint, clusterFqdn);
            var fabricClient = new FabricClient(xc, $"{clusterFqdn}:{ClusterManagementPort}");

            _logger.Info("Copying application package to image store...");
            fabricClient.ApplicationManager.CopyApplicationPackage(ImageStoreServiceConnectionString, packagePath, ApplicationTypeName);

            _logger.Info($"Provisioning application type {ApplicationTypeName}...");
            await fabricClient.ApplicationManager.ProvisionApplicationAsync(ApplicationTypeName);

            _logger.Info($"Creating application instance of type {ApplicationTypeName}...");
            var appDesc = new ApplicationDescription(new Uri($"fabric:/{ApplicationTypeName}"), ApplicationTypeName, "1.0.0");
            await fabricClient.ApplicationManager.CreateApplicationAsync(appDesc);

            _logger.Info("Service fabric application deployed successfully.");
            return 0;
        }

        private static X509Credentials GetCredentials(string clientCertThumb, string serverCertThumb, string name)
        {
            var x509Credentials = new X509Credentials
            {
                StoreLocation = StoreLocation.CurrentUser,
                StoreName = "My",
                FindType = X509FindType.FindByThumbprint,
                FindValue = clientCertThumb
            };
            x509Credentials.RemoteCommonNames.Add(name);
            x509Credentials.RemoteCertThumbprints.Add(serverCertThumb);
            x509Credentials.ProtectionLevel = ProtectionLevel.EncryptAndSign;
            return x509Credentials;
        }

        private static string GetResource(Assembly assembly, string fileName)
        {
            var resourceName = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith(fileName));
            using (var manifestResourceStream = assembly.GetManifestResourceStream(resourceName))
            using (var streamReader = new StreamReader(manifestResourceStream))
            {
                return streamReader.ReadToEnd();
            }
        }

        private string CreateApplicationPackage()
        {
            var packagePath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
            _logger.Info($"Packaging service fabric application at {packagePath}...");

            var assembly = Assembly.GetExecutingAssembly();

            var appPath = Path.Combine(packagePath, ApplicationTypeName);
            Directory.CreateDirectory(appPath);
            File.WriteAllText(Path.Combine(appPath, ApplicationManifestName), GetResource(assembly, ApplicationManifestName));

            var svcPath = Path.Combine(appPath, ApplicationTypeName);
            Directory.CreateDirectory(svcPath);
            File.WriteAllText(Path.Combine(svcPath, ServiceManifestName), GetResource(assembly, ServiceManifestName));

            var configPath = Path.Combine(svcPath, "Config");
            Directory.CreateDirectory(configPath);
            File.WriteAllText(Path.Combine(configPath, SettingsName), GetResource(assembly, SettingsName));

            var codePath = Path.Combine(svcPath, "Code");
            Directory.CreateDirectory(codePath);
            File.Copy(assembly.Location, Path.Combine(codePath, Path.GetFileName(assembly.Location)));

            _logger.Info("Package built");
            return packagePath;
        }
    }
}