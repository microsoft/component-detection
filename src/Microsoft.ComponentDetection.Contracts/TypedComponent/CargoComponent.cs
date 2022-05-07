using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class CargoComponent : TypedComponent
    {
        private CargoComponent()
        {
            // reserved for deserialization
        }

        public CargoComponent(string name, string version, string checksum = null, string source = null)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Cargo));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Cargo));
            Checksum = checksum;
            Source = source;
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public string Checksum { get; set; }

        public string Source { get; set; }

        public override ComponentType Type => ComponentType.Cargo;

        public override string Id
        {
            get {
                if (Source != null)
                {
                    return $"{Name} {Version} {Source} - {Type}";
                }
                else
                {
                    return $"{Name} {Version} - {Type}";
                }
            }
        }

        public override PackageURL PackageUrl => new PackageURL("cargo", Source ?? string.Empty, Name, Version, null, string.Empty);
    }
}
