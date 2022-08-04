using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class RubyGemsComponent : TypedComponent
    {
        private RubyGemsComponent()
        {
            /* Reserved for deserialization */
        }

        public RubyGemsComponent(string name, string version, string source = "")
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.RubyGems));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.RubyGems));
            Source = source;
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public string Source { get; set; }

        public override ComponentType Type => ComponentType.RubyGems;

        public override string Id => $"{Name} {Version} - {Type}";

        public override PackageURL PackageUrl => new PackageURL("gem", null, Name, Version, null, null);
    }
}
