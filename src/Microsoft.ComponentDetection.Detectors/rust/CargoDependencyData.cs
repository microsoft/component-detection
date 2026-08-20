#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust;

using System.Collections.Generic;

public class CargoDependencyData
{
    public CargoDependencyData()
    {
        this.CargoWorkspaces = [];
        this.CargoWorkspaceExclusions = [];
        this.NonDevDependencies = [];
        this.DevDependencies = [];
    }

    public HashSet<string> CargoWorkspaces { get; set; }

    public HashSet<string> CargoWorkspaceExclusions { get; set; }

    public IList<DependencySpecification> NonDevDependencies { get; set; }

    public IList<DependencySpecification> DevDependencies { get; set; }
}
