using PackageUrl;

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
            GroupId = ValidateRequiredInput(groupId, nameof(GroupId), nameof(ComponentType.Maven));
            ArtifactId = ValidateRequiredInput(artifactId, nameof(ArtifactId), nameof(ComponentType.Maven));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Maven));
        }

        public string GroupId { get; set; }

        public string ArtifactId { get; set; }

        public string Version { get; set; }

        public override ComponentType Type => ComponentType.Maven;

        public override string Id => $"{GroupId} {ArtifactId} {Version} - {Type}";

        public override PackageURL PackageUrl => new PackageURL("maven", GroupId, ArtifactId, Version, null, null);
    }
}
