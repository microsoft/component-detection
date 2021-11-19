using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class CargoComponent : TypedComponent
    {
        private CargoComponent()
        {
            // reserved for deserialization
        }

        public CargoComponent(string name, string version)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Cargo));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Cargo));
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public override ComponentType Type => ComponentType.Cargo;

        public override string Id => $"{Name} {Version} - {Type}";

        public override PackageURL PackageUrl => new PackageURL("cargo", string.Empty, Name, Version, null, string.Empty);
    }
}
