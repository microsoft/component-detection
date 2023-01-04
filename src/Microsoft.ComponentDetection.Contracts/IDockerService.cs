using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

namespace Microsoft.ComponentDetection.Contracts
{
    public interface IDockerService
    {
        Task<bool> CanRunLinuxContainersAsync(CancellationToken cancellationToken = default);

        Task<bool> CanPingDockerAsync(CancellationToken cancellationToken = default);

        Task<bool> ImageExistsLocallyAsync(string image, CancellationToken cancellationToken = default);

        Task<bool> TryPullImageAsync(string image, CancellationToken cancellationToken = default);

        Task<ContainerDetails> InspectImageAsync(string image, CancellationToken cancellationToken = default);

        Task<(string Stdout, string Stderr)> CreateAndRunContainerAsync(string image, IList<string> command, CancellationToken cancellationToken = default);
    }
}
