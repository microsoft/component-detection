using System;

namespace Microsoft.ComponentDetection.Contracts.TypedComponentNS
{
    public class OtherComponent : TypedComponent
    {
        public OtherComponent(string name, string version, Uri downloadUrl, string hash)
        {
            this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Other));
            this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Other));
            this.DownloadUrl = this.ValidateRequiredInput(downloadUrl, nameof(this.DownloadUrl), nameof(ComponentType.Other));
            this.Hash = hash;
        }

        public string Name { get; set; }

        public string Version { get; set; }

        public Uri DownloadUrl { get; set; }

        public string Hash { get; set; }

        public override ComponentType Type => ComponentType.Other;

        public override string Id => $"{this.Name} {this.Version} {this.DownloadUrl} - {this.Type}";

        private OtherComponent()
        {
            /* Reserved for deserialization */
        }
    }
}
