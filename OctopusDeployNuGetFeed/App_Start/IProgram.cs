using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed
{
    public interface IProgram
    {
        Task Main(string[] args);
    }
}