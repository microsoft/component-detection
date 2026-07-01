#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using PackageUrl;

public class GitComponent : TypedComponent
{
    private const string GithubHost = "github.com";
    private const string DotGitSuffix = ".git";

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

    /// <summary>
    /// Gets <c>pkg:github/{owner}/{repo}@{commit}</c> for repositories hosted on github.com whose
    /// path resolves cleanly to <c>owner/repo</c>; null for any other host (gitlab, bitbucket, ADO,
    /// GitHub Enterprise, etc.) or malformed paths. Consumers should fall back to
    /// <see cref="RepositoryUrl"/> when this returns null.
    /// </summary>
    [JsonPropertyName("packageUrl")]
    public override PackageURL PackageUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(this.CommitHash)
                || !TryGetGithubOwnerAndRepo(this.RepositoryUrl, out var owner, out var repo))
            {
                return null;
            }

            return new PackageURL("github", owner, repo, this.CommitHash, null, null);
        }
    }

    protected override string ComputeBaseId() => $"{this.RepositoryUrl} : {this.CommitHash} - {this.Type}";

    /// <summary>
    /// Suppresses the base impl so <see cref="TypedComponent.Id"/> stays stable if a detector later
    /// populates <see cref="TypedComponent.DownloadUrl"/> or <see cref="TypedComponent.SourceUrl"/>.
    /// RepositoryUrl and CommitHash are already in BaseId; the GitHub archive download URL is
    /// deterministic and source URL would duplicate RepositoryUrl.
    /// </summary>
    /// <returns>An empty sequence.</returns>
    protected override IEnumerable<KeyValuePair<string, string>> GetExtendedIdProperties()
    {
        yield break;
    }

    private static bool TryGetGithubOwnerAndRepo(Uri repositoryUrl, out string owner, out string repo)
    {
        owner = null;
        repo = null;

        if (repositoryUrl == null
            || !repositoryUrl.IsAbsoluteUri
            || !string.Equals(repositoryUrl.Host, GithubHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var trimmedPath = repositoryUrl.AbsolutePath?.Trim('/');
        if (string.IsNullOrEmpty(trimmedPath))
        {
            return false;
        }

        var segments = trimmedPath.Split('/');
        if (segments.Length != 2)
        {
            return false;
        }

        var ownerSegment = segments[0];
        var repoSegment = segments[1];
        if (repoSegment.EndsWith(DotGitSuffix, StringComparison.OrdinalIgnoreCase))
        {
            repoSegment = repoSegment[..^DotGitSuffix.Length];
        }

        if (string.IsNullOrEmpty(ownerSegment) || string.IsNullOrEmpty(repoSegment))
        {
            return false;
        }

        owner = ownerSegment;
        repo = repoSegment;
        return true;
    }
}
