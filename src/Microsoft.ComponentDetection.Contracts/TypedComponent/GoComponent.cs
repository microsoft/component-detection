using System;
using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class GoComponent : TypedComponent, IEquatable<GoComponent>
    {
        private GoComponent()
        {
            /* Reserved for deserialization */
        }

        public GoComponent(string name, string version)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Go));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Go));
            Hash = string.Empty;
        }

        public GoComponent(string name, string version, string hash)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Go));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Go));
            Hash = ValidateRequiredInput(hash, nameof(Hash), nameof(ComponentType.Go));
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public string Hash { get; set; }

        public override ComponentType Type => ComponentType.Go;

        public override string Id => $"{Name} {Version} - {Type}";

        public override bool Equals(object other)
        {
            GoComponent otherComponent = other as GoComponent;
            return otherComponent != null && Equals(otherComponent);
        }

        public bool Equals(GoComponent other)
        {
            if (other == null)
            {
                return false;
            }

            return Name == other.Name &&
                   Version == other.Version &&
                   Hash == other.Hash;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Version.GetHashCode() ^ Hash.GetHashCode();
        }

        // Commit should be used in place of version when available
        // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L610
        public override PackageURL PackageUrl => new PackageURL("golang", null, Name, string.IsNullOrWhiteSpace(Hash) ? Version : Hash, null, null);
    }
}
