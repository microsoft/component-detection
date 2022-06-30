namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class DockerReferenceComponent : TypedComponent
    {
        private DockerReferenceComponent()
        {
            /* Reserved for deserialization */
        }

        public DockerReferenceComponent(string hash, string name = null, string tag = null)
        {
            Digest = ValidateRequiredInput(hash, nameof(Digest), nameof(ComponentType.DockerReference));
            Name = name;
            Tag = tag;
        }

        public DockerReferenceComponent(DockerReference reference)
        {
        }

        public string Name { get; set; }

        public string Digest { get; set; }

        public string Tag { get; set; }

        public string Domain { get; set; }

        public override ComponentType Type => ComponentType.DockerReference;

        public DockerReference FullReference
        {
            get
            {
                return DockerReference.CreateDockerReference(Name, Domain, Digest, Tag);
            }
        }

        public override string Id => $"{Name} {Tag} {Digest}";
    }
}