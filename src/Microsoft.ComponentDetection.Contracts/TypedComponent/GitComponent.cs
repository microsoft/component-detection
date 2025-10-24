#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;

public class GitComponent : TypedComponent
{
    public GitComponent(Uri repositoryUrl, string commitHash)
    {
        this.RepositoryUrl = this.ValidateRequiredInput(repositoryUrl, nameof(this.RepositoryUrl), nameof(ComponentType.Git));
        this.CommitHash = this.ValidateRequiredInput(commitHash, nameof(this.CommitHash), nameof(ComponentType.Git));
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

    protected override string ComputeId() => $"{this.RepositoryUrl} : {this.CommitHash} - {this.Type}";
}
