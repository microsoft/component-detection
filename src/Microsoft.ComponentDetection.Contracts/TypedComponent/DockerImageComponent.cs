namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class DockerImageComponent : TypedComponent
    {
        public DockerImageComponent(string hash, string name = null, string tag = null)
        {
            this.Digest = this.ValidateRequiredInput(hash, nameof(this.Digest), nameof(ComponentType.DockerImage));
            this.Name = name;
            this.Tag = tag;
        }

        public string Name { get; set; }

        public string Digest { get; set; }

        public string Tag { get; set; }

        public override ComponentType Type => ComponentType.DockerImage;

        public override string Id => $"{this.Name} {this.Tag} {this.Digest}";

        private DockerImageComponent()
        {
            /* Reserved for deserialization */
        }
    }
}
