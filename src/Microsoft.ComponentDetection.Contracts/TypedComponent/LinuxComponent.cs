using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class LinuxComponent : TypedComponent
    {
        private LinuxComponent()
        {
            /* Reserved for deserialization */
        }

        public LinuxComponent(string distribution, string release, string name, string version, string sourceName)
        {
            Distribution = ValidateRequiredInput(distribution, nameof(Distribution), nameof(ComponentType.Linux));
            Release = ValidateRequiredInput(release, nameof(Release), nameof(ComponentType.Linux));
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Linux));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Linux));
            SourceName = sourceName;
        }

        public string Distribution { get; set; }

        public string Release { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        public string SourceName { get; set; }

        public override ComponentType Type => ComponentType.Linux;

        public override string Id => $"{Distribution} {Release} {Name} {Version} - {Type}";

        public override PackageURL PackageUrl
        {
            get
            {
                string packageType = null;

                if (IsUbuntu() || IsDebian())
                {
                    packageType = "deb";
                }
                else if (IsCentOS() || IsFedora() || IsRHEL() || IsMariner())
                {
                    packageType = "rpm";
                }

                if (packageType != null)
                {
                    return new PackageURL(packageType, Distribution, Name, Version, null, null);
                }

                return null;
            }
        }

        private bool IsUbuntu()
        {
            return Distribution.ToLowerInvariant() == "ubuntu";
        }

        private bool IsDebian()
        {
            return Distribution.ToLowerInvariant() == "debian";
        }

        private bool IsCentOS()
        {
            return Distribution.ToLowerInvariant() == "centos";
        }

        private bool IsFedora()
        {
            return Distribution.ToLowerInvariant() == "fedora";
        }

        private bool IsRHEL()
        {
            return Distribution.ToLowerInvariant() == "red hat enterprise linux";
        }

        private bool IsMariner()
        {
            return Distribution.ToLowerInvariant() == "mariner";
        }
    }
}
