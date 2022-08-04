﻿using PackageUrl;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class MavenComponent : TypedComponent
    {
        private MavenComponent()
        {
            /* Reserved for deserialization */
        }

        public MavenComponent(string groupId, string artifactId, string version)
        {
            this.GroupId = this.ValidateRequiredInput(groupId, nameof(this.GroupId), nameof(ComponentType.Maven));
            this.ArtifactId = this.ValidateRequiredInput(artifactId, nameof(this.ArtifactId), nameof(ComponentType.Maven));
            this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Maven));
        }

        public string GroupId { get; set; }

        public string ArtifactId { get; set; }

        public string Version { get; set; }

        public override ComponentType Type => ComponentType.Maven;

        public override string Id => $"{this.GroupId} {this.ArtifactId} {this.Version} - {this.Type}";

        public override PackageURL PackageUrl => new PackageURL("maven", this.GroupId, this.ArtifactId, this.Version, null, null);
    }
}
