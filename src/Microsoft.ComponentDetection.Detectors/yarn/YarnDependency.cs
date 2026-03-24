#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn;

public class YarnDependency
{
    public string LookupKey => $"{this.Name}@{this.Version}";

    public string Name { get; set; }

    public string Version { get; set; }
}
