namespace Microsoft.ComponentDetection.Detectors.Pip;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

[Export(typeof(IComponentDetector))]
public class PipComponentDetector : FileComponentDetector
{
    public override string Id => "Pip";

    public override IList<string> SearchPatterns => new List<string> { "setup.py", "requirements.txt" };

    public override IEnumerable<string> Categories => new List<string> { "Python" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Pip };

    public override int Version { get; } = 6;

    [Import]
    public IPythonCommandService PythonCommandService { get; set; }

    [Import]
    public IPythonResolver PythonResolver { get; set; }

    protected override async Task<IObservable<ProcessRequest>> OnPrepareDetectionAsync(IObservable<ProcessRequest> processRequests, IDictionary<string, string> detectorArgs)
    {
        this.CurrentScanRequest.DetectorArgs.TryGetValue("Pip.PythonExePath", out var pythonExePath);
        if (!await this.PythonCommandService.PythonExistsAsync(pythonExePath))
        {
            this.Logger.LogInfo($"No python found on system. Python detection will not run.");

            return Enumerable.Empty<ProcessRequest>().ToObservable();
        }

        return processRequests;
    }

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        this.CurrentScanRequest.DetectorArgs.TryGetValue("Pip.PythonExePath", out var pythonExePath);
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        try
        {
            var initialPackages = await this.PythonCommandService.ParseFileAsync(file.Location, pythonExePath);
            var listedPackage = initialPackages.Where(tuple => tuple.PackageString != null)
                .Select(tuple => tuple.PackageString)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new PipDependencySpecification(x))
                .Where(x => !x.PackageIsUnsafe())
                .ToList();

            var roots = await this.PythonResolver.ResolveRootsAsync(singleFileComponentRecorder, listedPackage);

            RecordComponents(
                singleFileComponentRecorder,
                roots);

            initialPackages.Where(tuple => tuple.Component != null)
                .Select(tuple => new DetectedComponent(tuple.Component))
                .ToList()
                .ForEach(gitComponent => singleFileComponentRecorder.RegisterUsage(gitComponent, isExplicitReferencedDependency: true));
        }
        catch (Exception e)
        {
            this.Logger.LogFailedReadingFile(file.Location, e);
        }
    }

    private static void RecordComponents(
        ISingleFileComponentRecorder recorder,
        IList<PipGraphNode> roots)
    {
        var nonRoots = new Queue<(DetectedComponent, PipGraphNode)>();

        var explicitRoots = roots.Select(a => a.Value).ToHashSet();

        foreach (var root in roots)
        {
            var rootDetectedComponent = new DetectedComponent(root.Value);

            recorder.RegisterUsage(
                rootDetectedComponent,
                isExplicitReferencedDependency: true);

            foreach (var child in root.Children)
            {
                nonRoots.Enqueue((rootDetectedComponent, child));
            }
        }

        var registeredIds = new HashSet<string>();

        while (nonRoots.Count > 0)
        {
            var (parent, item) = nonRoots.Dequeue();

            var detectedComponent = new DetectedComponent(item.Value);

            recorder.RegisterUsage(
                detectedComponent,
                parentComponentId: parent.Component.Id);

            if (!registeredIds.Contains(detectedComponent.Component.Id))
            {
                foreach (var child in item.Children)
                {
                    nonRoots.Enqueue((detectedComponent, child));
                }

                registeredIds.Add(detectedComponent.Component.Id);
            }
        }
    }
}
