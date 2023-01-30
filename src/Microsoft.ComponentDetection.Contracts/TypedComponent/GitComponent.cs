namespace Microsoft.ComponentDetection.Contracts.TypedComponent;
using System;

public class GitComponent : TypedComponent
{
    public GitComponent(Uri repositoryUrl, string commitHash)
    {
        this.RepositoryUrl = ValidateRequiredInput(repositoryUrl, nameof(this.RepositoryUrl), nameof(ComponentType.Git));
        this.CommitHash = ValidateRequiredInput(commitHash, nameof(this.CommitHash), nameof(ComponentType.Git));
    }

    public GitComponent(Uri repositoryUrl, string commitHash, string tag)
        : this(repositoryUrl, commitHash) => this.Tag = tag;

    private GitComponent()
    {
        /* Reserved for deserialization */
    }

    public Uri RepositoryUrl { get; set; }

    public string CommitHash { get; set; }

    public string Tag { get; set; }

    public override ComponentType Type => ComponentType.Git;

    public override string Id => $"{this.RepositoryUrl} : {this.CommitHash} - {this.Type}";
}
