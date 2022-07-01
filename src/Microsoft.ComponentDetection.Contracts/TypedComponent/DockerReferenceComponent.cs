namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class DockerReferenceComponent : TypedComponent
    {
        private DockerReferenceComponent()
        {
            /* Reserved for deserialization */
        }

        public DockerReferenceComponent(string hash, string repository = null, string tag = null)
        {
            Digest = ValidateRequiredInput(hash, nameof(Digest), nameof(ComponentType.DockerReference));
            Repository = repository;
            Tag = tag;
        }

        public DockerReferenceComponent(DockerReference reference)
        {
        }

        public string Repository { get; set; }

        public string Digest { get; set; }

        public string Tag { get; set; }

        public string Domain { get; set; }

        public override ComponentType Type => ComponentType.DockerReference;

        public DockerReference FullReference
        {
            get
            {
                return DockerReference.CreateDockerReference(Repository, Domain, Digest, Tag);
            }
        }

        public override string Id => $"{Repository} {Tag} {Digest}";
    }
}