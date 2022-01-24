using Microsoft.ComponentDetection.Contracts.Internal;
using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class NpmComponent : TypedComponent
    {
        private NpmComponent()
        {
            /* Reserved for deserialization */
        }

        public NpmComponent(string name, string version, string hash = null)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Npm));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Npm));
            Hash = hash; // Not required; only found in package-lock.json, not package.json
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public string Hash { get; set; }

        public NpmAuthor Author { get; set; }

        public override ComponentType Type => ComponentType.Npm;

        public override string Id => $"{Name} {Version} - {Type}";

        public override PackageURL PackageUrl => new PackageURL("npm", null, Name, Version, null, null);
    }
}
