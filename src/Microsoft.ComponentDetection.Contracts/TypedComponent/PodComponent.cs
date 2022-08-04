using PackageUrl;
using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class PodComponent : TypedComponent
    {
        private PodComponent()
        {
            /* Reserved for deserialization */
        }

        public PodComponent(string name, string version, string specRepo = "")
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Pod));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Pod));
            SpecRepo = specRepo;
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public string SpecRepo { get; set; }

        public override ComponentType Type => ComponentType.Pod;

        public override string Id => $"{Name} {Version} - {Type}";

        public override PackageURL PackageUrl
        {
            get
            {
                var qualifiers = new SortedDictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(SpecRepo))
                {
                    qualifiers.Add("repository_url", SpecRepo);
                }

                return new PackageURL("cocoapods", null, Name, Version, qualifiers, null);
            }
        }
    }
}
