using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class CargoComponent : TypedComponent
    {
        private CargoComponent()
        {
            // reserved for deserialization
        }

        public CargoComponent(string name, string version, string source, string checksum = null)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Cargo));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Cargo));
            Source = ValidateRequiredInput(source, nameof(Source), nameof(ComponentType.Cargo));
            Checksum = checksum;
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public string Checksum { get; set; }

        public string Source { get; set; }

        public override ComponentType Type => ComponentType.Cargo;

        public override string Id => $"{Name} {Version} ({Source}) - {Type}";

        public override PackageURL PackageUrl => new PackageURL("cargo", Source, Name, Version, null, string.Empty);
    }
}
