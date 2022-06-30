namespace Microsoft.ComponentDetection.Contracts
{
#pragma warning disable SA1402
    public class DockerReference
    {
        public virtual DockerReferenceKind Kind { get; }

        public virtual TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            throw new System.NotImplementedException();
        }

        public static DockerReference CreateDockerReference(string repository, string domain, string digest, string tag)
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
                if (string.IsNullOrEmpty(digest))
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
    }

    public enum DockerReferenceKind
    {
        Canonical = 0,
        Repository = 1,
        Tagged = 2,
        Dual = 3,
        Digest = 4,
    }

    public class Reference
    {
        public string Tag { get; set; }

        public string Digest { get; set; }

        public string Repository { get; set; }

        public string Domain { get; set; }
    }

    // sha256:abc123...
    public class DigestReference : DockerReference
    {
        public override DockerReferenceKind Kind { get; } = DockerReferenceKind.Digest;

        public string Digest;

        public override string ToString()
        {
            return $"{Digest}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Digest = Digest,
            };
        }
    }

    // docker.io/library/ubuntu@sha256:abc123...
    public class CanonicalReference : DockerReference
    {
        public override DockerReferenceKind Kind { get; } = DockerReferenceKind.Canonical;

        public string Domain;

        public string Repository;

        public string Digest;

        public override string ToString()
        {
            return $"{Domain}/{Repository}@${Digest}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Domain = Domain,
                Digest = Digest,
                Name = Repository,
            };
        }
    }

    // docker.io/library/ubuntu
    public class RepositoryReference : DockerReference
    {
        public override DockerReferenceKind Kind { get; } = DockerReferenceKind.Repository;

        public string Domain;

        public string Repository;

        public override string ToString()
        {
            return $"{Repository}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Domain = Domain,
                Name = Repository,
            };
        }
    }

    // docker.io/library/ubuntu:latest
    public class TaggedReference : DockerReference
    {
        public override DockerReferenceKind Kind { get; } = DockerReferenceKind.Tagged;

        public string Domain;

        public string Repository;

        public string Tag;

        public override string ToString()
        {
            return $"{Domain}/{Repository}:${Tag}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Domain = Domain,
                Tag = Tag,
                Name = Repository,
            };
        }
    }

    // docker.io/library/ubuntu:latest@sha256:abc123...
    public class DualReference : DockerReference
    {
        public override DockerReferenceKind Kind { get; } = DockerReferenceKind.Dual;

        public string Domain;

        public string Repository;

        public string Tag;

        public string Digest;

        public override string ToString()
        {
            return $"{Domain}/{Repository}:${Tag}@${Digest}";
        }

        public override TypedComponent.DockerReferenceComponent ToTypedDockerReferenceComponent()
        {
            return new TypedComponent.DockerReferenceComponent(this)
            {
                Domain = Domain,
                Digest = Digest,
                Tag = Tag,
                Name = Repository,
            };
        }
    }
}