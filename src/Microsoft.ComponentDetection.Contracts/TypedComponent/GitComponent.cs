#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Text.Json.Serialization;

public class GitComponent : TypedComponent
{
    public GitComponent(Uri repositoryUrl, string commitHash)
    {
        this.RepositoryUrl = this.ValidateRequiredInput(repositoryUrl, nameof(this.RepositoryUrl), nameof(ComponentType.Git));
        this.CommitHash = this.ValidateRequiredInput(commitHash, nameof(this.CommitHash), nameof(ComponentType.Git));
    }

    public GitComponent(Uri repositoryUrl, string commitHash, string tag)
        : this(repositoryUrl, commitHash) => this.Tag = tag;

    public GitComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("repositoryUrl")]
    public Uri RepositoryUrl { get; set; }

    [JsonPropertyName("commitHash")]
    public string CommitHash { get; set; }

    [JsonPropertyName("tag")]
    public string Tag { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Git;

    protected override string ComputeId() => $"{this.RepositoryUrl} : {this.CommitHash} - {this.Type}";
}
