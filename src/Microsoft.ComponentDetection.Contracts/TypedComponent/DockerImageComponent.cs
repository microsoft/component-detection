namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class DockerImageComponent : TypedComponent
    {
        private DockerImageComponent()
        {
            /* Reserved for deserialization */
        }

        public DockerImageComponent(string hash, string name = null, string tag = null)
        {
            Digest = ValidateRequiredInput(hash, nameof(Digest), nameof(ComponentType.DockerImage));
            Name = name;
            Tag = tag;
        }

        public string Name { get; set; }

        public string Digest { get; set; }

        public string Tag { get; set; }

        public override ComponentType Type => ComponentType.DockerImage;

        public override string Id => $"{Name} {Tag} {Digest}";
    }
}
