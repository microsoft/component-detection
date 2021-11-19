using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Rust
{
    public class CargoDependencyData
    {
        public HashSet<string> CargoWorkspaces;

        public HashSet<string> CargoWorkspaceExclusions;

        public IList<DependencySpecification> NonDevDependencies;

        public IList<DependencySpecification> DevDependencies;

        public CargoDependencyData()
        {
            CargoWorkspaces = new HashSet<string>();
            CargoWorkspaceExclusions = new HashSet<string>();
            NonDevDependencies = new List<DependencySpecification>();
            DevDependencies = new List<DependencySpecification>();
        }
    }
}
