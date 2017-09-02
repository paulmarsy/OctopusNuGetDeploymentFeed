using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed.Remoting
{
    /// <summary>
    ///     Class with extension helper methods for working with the Task class
    /// </summary>
    internal static class TaskExtensions
    {
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "task")]
        public static void Forget(this Task task)
        {
        }
    }
}