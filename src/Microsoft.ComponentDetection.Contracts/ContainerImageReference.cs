#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

public enum ContainerImageReferenceKind
{
    Canonical = 0,
    Repository = 1,
    Tagged = 2,
    Dual = 3,
    Digest = 4,
}

#pragma warning disable SA1402
public class ContainerImageReference
{
    public virtual ContainerImageReferenceKind Kind { get; }

    public static ContainerImageReference CreateContainerImageReference(string repository, string domain, string digest, string tag)
    {
        if (!string.IsNullOrEmpty(repository) && string.IsNullOrEmpty(domain))
        {
            if (!string.IsNullOrEmpty(digest))
            {
                return new DigestReference
                {
                    Digest = digest,
                };
            }
            else
            {
                throw new System.InvalidOperationException("Repository name must have at least one component");
            }
        }
        else if (string.IsNullOrEmpty(tag))
        {
            if (!string.IsNullOrEmpty(digest))
            {
                return new CanonicalReference
                {
                    Domain = domain,
                    Repository = repository,
                    Digest = digest,
                };
            }
            else
            {
                return new RepositoryReference
                {
                    Domain = domain,
                    Repository = repository,
                };
            }
        }
        else if (string.IsNullOrEmpty(digest))
        {
            return new TaggedReference
            {
                Domain = domain,
                Repository = repository,
                Tag = tag,
            };
        }
        else
        {
            return new DualReference
            {
                Domain = domain,
                Repository = repository,
                Tag = tag,
                Digest = digest,
            };
        }
    }

    public virtual TypedComponent.ContainerImageReferenceComponent ToTypedContainerImageReferenceComponent()
    {
        throw new System.NotImplementedException();
    }
}

public class Reference
{
    public string Tag { get; set; }

    public string Digest { get; set; }

    public string Repository { get; set; }

    public string Domain { get; set; }
}

// sha256:abc123...
public class DigestReference : ContainerImageReference
{
    public string Digest { get; set; }

    public override ContainerImageReferenceKind Kind { get; } = ContainerImageReferenceKind.Digest;

    public override string ToString()
    {
        return $"{this.Digest}";
    }

    public override TypedComponent.ContainerImageReferenceComponent ToTypedContainerImageReferenceComponent()
    {
        return new TypedComponent.ContainerImageReferenceComponent(this)
        {
            Digest = this.Digest,
        };
    }
}

// docker.io/library/ubuntu@sha256:abc123...
public class CanonicalReference : ContainerImageReference
{
    public string Domain { get; set; }

    public string Repository { get; set; }

    public string Digest { get; set; }

    public override ContainerImageReferenceKind Kind { get; } = ContainerImageReferenceKind.Canonical;

    public override string ToString()
    {
        return $"{this.Domain}/{this.Repository}@${this.Digest}";
    }

    public override TypedComponent.ContainerImageReferenceComponent ToTypedContainerImageReferenceComponent()
    {
        return new TypedComponent.ContainerImageReferenceComponent(this)
        {
            Domain = this.Domain,
            Digest = this.Digest,
            Repository = this.Repository,
        };
    }
}

// docker.io/library/ubuntu
public class RepositoryReference : ContainerImageReference
{
    public string Domain { get; set; }

    public string Repository { get; set; }

    public override ContainerImageReferenceKind Kind { get; } = ContainerImageReferenceKind.Repository;

    public override string ToString()
    {
        return $"{this.Repository}";
    }

    public override TypedComponent.ContainerImageReferenceComponent ToTypedContainerImageReferenceComponent()
    {
        return new TypedComponent.ContainerImageReferenceComponent(this)
        {
            Domain = this.Domain,
            Repository = this.Repository,
        };
    }
}

// docker.io/library/ubuntu:latest
public class TaggedReference : ContainerImageReference
{
    public string Domain { get; set; }

    public string Repository { get; set; }

    public string Tag { get; set; }

    public override ContainerImageReferenceKind Kind { get; } = ContainerImageReferenceKind.Tagged;

    public override string ToString()
    {
        return $"{this.Domain}/{this.Repository}:${this.Tag}";
    }

    public override TypedComponent.ContainerImageReferenceComponent ToTypedContainerImageReferenceComponent()
    {
        return new TypedComponent.ContainerImageReferenceComponent(this)
        {
            Domain = this.Domain,
            Tag = this.Tag,
            Repository = this.Repository,
        };
    }
}

// docker.io/library/ubuntu:latest@sha256:abc123...
public class DualReference : ContainerImageReference
{
    public string Domain { get; set; }

    public string Repository { get; set; }

    public string Tag { get; set; }

    public string Digest { get; set; }

    public override ContainerImageReferenceKind Kind { get; } = ContainerImageReferenceKind.Dual;

    public override string ToString()
    {
        return $"{this.Domain}/{this.Repository}:${this.Tag}@${this.Digest}";
    }

    public override TypedComponent.ContainerImageReferenceComponent ToTypedContainerImageReferenceComponent()
    {
        return new TypedComponent.ContainerImageReferenceComponent(this)
        {
            Domain = this.Domain,
            Digest = this.Digest,
            Tag = this.Tag,
            Repository = this.Repository,
        };
    }
}
