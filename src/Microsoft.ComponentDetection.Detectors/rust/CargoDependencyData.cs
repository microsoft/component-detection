namespace Microsoft.ComponentDetection.Detectors.Rust
{
    using System.Collections.Generic;

    public class CargoDependencyData
    {
        public HashSet<string> CargoWorkspaces;

        public HashSet<string> CargoWorkspaceExclusions;

        public IList<DependencySpecification> NonDevDependencies;

        public IList<DependencySpecification> DevDependencies;

        public CargoDependencyData()
        {
            this.CargoWorkspaces = new HashSet<string>();
            this.CargoWorkspaceExclusions = new HashSet<string>();
            this.NonDevDependencies = new List<DependencySpecification>();
            this.DevDependencies = new List<DependencySpecification>();
        }
    }
}
