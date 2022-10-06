using System.Collections.Generic;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;

namespace Microsoft.ComponentDetection.OrchestratorNS.Services
{
    public class DetectorProcessingResult
    {
        public ProcessingResultCode ResultCode { get; set; }

        public Dictionary<int, ContainerDetails> ContainersDetailsMap { get; set; }

        public IEnumerable<(IComponentDetector detector, ComponentRecorder recorder)> ComponentRecorders { get; set; }
    }
}
