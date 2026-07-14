namespace Microsoft.ComponentDetection.Detectors.Uv;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

internal class UvPackage
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public List<UvDependency> Dependencies { get; set; } = [];

    // Metadata dependencies (requires-dist)
    public List<UvDependency> MetadataRequiresDist { get; set; } = [];

    // Metadata dev dependencies (requires-dev)
    public List<UvDependency> MetadataRequiresDev { get; set; } = [];

    // Source property for uv.lock
    public UvSource? Source { get; set; }

    public TypedComponent ToTypedComponent()
    {
        if (this.Source?.Git != null)
        {
            var (repoUrl, commitHash) = UvLockComponentDetector.ParseGitUrl(this.Source.Git);
            return new GitComponent(repoUrl, commitHash);
        }

        return new PipComponent(this.Name, this.Version);
    }
}
