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

        public DockerImageComponent(IDockerReference reference){
            FullReference = reference;
        }      

        public string Name { get; set; }

        public string Digest { get; set; }

        public string Tag { get; set; }

        public string Domain { get; set; }

        public override ComponentType Type => ComponentType.DockerImage;

        public IDockerReference FullReference { get; set; }

        public override string Id => $"{Name} {Tag} {Digest}";
    }
}