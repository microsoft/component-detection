using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class PubComponent : TypedComponent
    {
        private PubComponent()
        {
            /* Reserved for deserialization */
        }

        public PubComponent(string name, string version)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Pip));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Pip));
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public override ComponentType Type => ComponentType.Pip;

        public override string Id => $"{Name} {Version} - {Type}".ToLowerInvariant();

        public override PackageURL PackageUrl => new PackageURL("pub", null, Name, Version, null, null);
    }
}
