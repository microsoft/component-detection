namespace Microsoft.ComponentDetection.Contracts
{
    #pragma warning disable SA1402
    public abstract class IDockerReference
    {
        public abstract string type { get; }

        public abstract TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent();
    }

    public class Reference
    {
        public string Tag { get; set; }
        public string Digest { get; set; }
        public string Repository { get; set; }
        public string Domain { get; set; }
    }

    // sha256:abc123...
    public class DigestReference : IDockerReference
    {
        public override string type { get; } = "digest";
        public string digest;

        public override string ToString()
        {
            return $"{this.digest}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Digest = this.digest,
            };
        }
    }

    // docker.io/library/ubuntu@sha256:abc123...
    public class CanonicalReference : IDockerReference
    {
        public override string type { get; } = "canonical";
        public string domain;
        public string repository;
        public string digest;

        public override string ToString()
        {
            return $"{this.repository}@${this.digest}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Domain = this.domain,
                Digest = this.digest,
                Name = this.repository,
            };
        }
    }

    // docker.io/library/ubuntu
    public class RepositoryReference : IDockerReference
    {
        public override string type { get; } = "repository";
        public string domain;
        public string repository;
        public override string ToString()
        {
            return $"{this.repository}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Domain = this.domain,
                Name = this.repository,
            };
        }
    }

    // docker.io/library/ubuntu:latest
    public class TaggedReference : IDockerReference
    {
        public override string type { get; } = "tagged";
        public string domain;
        public string repository;
        public string tag;
        public override string ToString()
        {
            return $"{this.repository}:${this.tag}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Domain = this.domain,
                Tag = this.tag,
                Name = this.repository,
            };
        }
    }

    // docker.io/library/ubuntu:latest@sha256:abc123...
    public class DualReference : IDockerReference
    {
        public override string type { get; } = "dual";
        public string domain;
        public string repository;
        public string tag;
        public string digest;

        public override string ToString()
        {
            return $"{this.repository}:${this.tag}@${this.digest}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Domain = this.domain,
                Digest = this.digest,
                Tag = this.tag,
                Name = this.repository,
            };
        }
    }
}