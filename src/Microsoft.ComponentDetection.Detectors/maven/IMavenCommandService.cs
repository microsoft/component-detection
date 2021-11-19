using Microsoft.ComponentDetection.Contracts.Internal;
using System.Threading.Tasks;

namespace Microsoft.ComponentDetection.Detectors.Maven
{
    public interface IMavenCommandService
    {
        string BcdeMvnDependencyFileName { get; }

        Task<bool> MavenCLIExists();

        Task GenerateDependenciesFile(ProcessRequest processRequest);

        void ParseDependenciesFile(ProcessRequest processRequest);
    }
}
