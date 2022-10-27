using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class PipComponent : TypedComponent
    {
        private PipComponent()
        {
            /* Reserved for deserialization */
        }

        public PipComponent(string name, string version)
        {
            this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Pip));
            this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Pip));
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public override ComponentType Type => ComponentType.Pip;

        public override string Id => $"{this.Name} {this.Version} - {this.Type}".ToLowerInvariant();

        public override PackageURL PackageUrl => new PackageURL("pypi", null, this.Name, this.Version, null, null);
    }
}
