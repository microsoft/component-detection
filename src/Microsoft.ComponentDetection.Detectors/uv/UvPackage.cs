namespace Microsoft.ComponentDetection.Detectors.Uv;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

internal class UvPackage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("dependencies")]
    public List<UvDependency> Dependencies { get; set; } = [];

    [JsonPropertyName("metadata")]
    public UvMetadata? Metadata { get; set; }

    [JsonPropertyName("source")]
    public UvSource? Source { get; set; }

    [JsonIgnore]
    public List<UvDependency> MetadataRequiresDist => this.Metadata?.RequiresDist ?? [];

    [JsonIgnore]
    public List<UvDependency> MetadataRequiresDev => this.Metadata?.RequiresDev?.Values
        .Where(group => group != null)
        .SelectMany(group => group!)
        .ToList() ?? [];

    public TypedComponent ToTypedComponent()
    {
        if (this.Source?.Git != null)
        {
            var (repoUrl, commitHash) = ParseGitUrl(this.Source.Git);
            return new GitComponent(repoUrl, commitHash);
        }

        return new PipComponent(this.Name, this.Version);
    }

    public void Normalize()
    {
        this.Dependencies = this.Dependencies
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .ToList();

        this.Metadata?.Normalize();
    }

    private static (Uri RepositoryUrl, string CommitHash) ParseGitUrl(string gitUrl)
    {
        var uri = new Uri(gitUrl);
        var repoUrl = new Uri(uri.GetLeftPart(UriPartial.Path));
        var commitHash = uri.Fragment.TrimStart('#');
        return (repoUrl, commitHash);
    }
}
