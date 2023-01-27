namespace Microsoft.ComponentDetection.Detectors.Rust;
using System.Collections.Generic;

public class CargoDependencyData
{
    public CargoDependencyData()
    {
        this.CargoWorkspaces = new HashSet<string>();
        this.CargoWorkspaceExclusions = new HashSet<string>();
        this.NonDevDependencies = new List<DependencySpecification>();
        this.DevDependencies = new List<DependencySpecification>();
    }

    public HashSet<string> CargoWorkspaces { get; set; }

    public HashSet<string> CargoWorkspaceExclusions { get; set; }

    public IList<DependencySpecification> NonDevDependencies { get; set; }

    public IList<DependencySpecification> DevDependencies { get; set; }
}
