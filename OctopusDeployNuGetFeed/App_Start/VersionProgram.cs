using System;
using System.Reflection;
using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed
{
    public class VersionProgram : IProgram
    {
        public const string Parameter = "version";

        public static string Version
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        public Task Main(string[] args)
        {
            Console.Write(Version);
            return Task.CompletedTask;
        }
    }
}