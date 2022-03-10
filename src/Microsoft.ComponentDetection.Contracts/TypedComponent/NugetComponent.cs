﻿using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class NuGetComponent : TypedComponent
    {
        private NuGetComponent()
        {
            /* Reserved for deserialization */
        }

        public NuGetComponent(string name, string version, string[] authors = null)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.NuGet));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.NuGet));
            Authors = authors;
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public string[] Authors { get; set; }

        public override ComponentType Type => ComponentType.NuGet;

        public override string Id => $"{Name} {Version} - {Type}";

        public override PackageURL PackageUrl => new PackageURL("nuget", null, Name, Version, null, null);
    }
}
