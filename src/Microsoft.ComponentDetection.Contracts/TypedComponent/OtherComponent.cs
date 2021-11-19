using System;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class OtherComponent : TypedComponent
    {
        private OtherComponent()
        {
            /* Reserved for deserialization */
        }

        public OtherComponent(string name, string version, Uri downloadUrl, string hash)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Other));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Other));
            DownloadUrl = ValidateRequiredInput(downloadUrl, nameof(DownloadUrl), nameof(ComponentType.Other));
            Hash = hash;
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public Uri DownloadUrl { get; set; }

        public string Hash { get; set; }

        public override ComponentType Type => ComponentType.Other;

        public override string Id => $"{Name} {Version} {DownloadUrl} - {Type}";
    }
}