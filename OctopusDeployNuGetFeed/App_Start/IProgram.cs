using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed
{
    public interface IProgram
    {
        Task<int> Main(string[] args);
    }
}