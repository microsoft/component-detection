using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class VcpkgComponent : TypedComponent
    {
        private VcpkgComponent()
        {
            /* Reserved for deserialization */
        }

        public VcpkgComponent(string spdxid, string name, string version, string triplet = null, string portVersion = null, string description = null, string downloadLocation = null)
        {
            int.TryParse(portVersion, out var port);

            SPDXID = ValidateRequiredInput(spdxid, nameof(SPDXID), nameof(ComponentType.Vcpkg));
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Vcpkg));
            Version = version;
            PortVersion = port;
            Triplet = triplet;
            Description = description;
            DownloadLocation = downloadLocation;
        }

        public string SPDXID { get; set; }

        public string Name { get; set; }

        public string DownloadLocation { get; set; }

        public string Triplet { get; set; }

        public string Version { get; set; }

        public string Description { get; set; }

        public int PortVersion { get; set; }

        public override ComponentType Type => ComponentType.Vcpkg;

        public override string Id
        {
            get
            {
                if (PortVersion > 0)
                {
                    return $"{Name} {Version}#{PortVersion} - {Type}";
                }
                else
                {
                    return $"{Name} {Version} - {Type}";
                }
            }
        }

        public override PackageURL PackageUrl
        {
            get
            {
                if (PortVersion > 0)
                {
                    return new PackageURL($"pkg:vcpkg/{Name}@{Version}?port_version={PortVersion}");
                }
                else if (Version != null)
                {
                    return new PackageURL($"pkg:vcpkg/{Name}@{Version}");
                }
                else
                {
                    return new PackageURL($"pkg:vcpkg/{Name}");
                }
            }
        }
    }
}
