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
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Pip));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Pip));
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public override ComponentType Type => ComponentType.Pip;

        public override string Id => $"{Name} {Version} - {Type}".ToLowerInvariant();

        public override PackageURL PackageUrl => new PackageURL("pypi", null, Name, Version, null, null);
    }
}
