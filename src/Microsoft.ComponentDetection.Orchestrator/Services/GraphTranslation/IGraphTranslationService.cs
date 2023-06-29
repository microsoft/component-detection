namespace Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;

using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Orchestrator.Commands;

public interface IGraphTranslationService
{
    /// <summary>
    /// Merges the results of multiple detectors into a single <see cref="ScanResult"/>, building the dependency graph.
    /// </summary>
    /// <param name="detectorProcessingResult">The detector processing result.</param>
    /// <param name="settings">The detector arguments.</param>
    /// <param name="updateLocations"><c>true</c> to set the component's locations found at. This should typically be set to true.
    /// Since the components are passed by reference, any updates to the components will be propogated globally.</param>
    /// <returns>A <see cref="ScanResult"/> with the final output of component detection.</returns>
    ScanResult GenerateScanResultFromProcessingResult(DetectorProcessingResult detectorProcessingResult, ScanSettings settings, bool updateLocations = true);
}
