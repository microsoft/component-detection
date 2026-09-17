#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using PackageUrl;

/// <summary>
/// Represents a Swift package manager component.
/// </summary>
public class SwiftComponent : TypedComponent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftComponent"/> class.
    /// </summary>
    /// <param name="name">The name of the component.</param>
    /// <param name="version">The version of the component.</param>
    /// <param name="repositoryUrl">The source repository URL of the component.</param>
    /// <param name="hash">The Git commit hash of the component.</param>
    /// <param name="kind">The Swift package kind.</param>
    public SwiftComponent(string name, string version, string repositoryUrl, string hash, string kind)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(name), nameof(ComponentType.Swift));
        this.Version = this.ValidateRequiredInput(version, nameof(version), nameof(ComponentType.Swift));
        this.ValidateRequiredInput(repositoryUrl, nameof(repositoryUrl), nameof(ComponentType.Swift));
        this.RepositoryUrl = new Uri(repositoryUrl);
        this.Kind = this.ValidateRequiredInput(kind, nameof(kind), nameof(ComponentType.Swift));
        this.CommitHash = this.ValidateRequiredInput(hash, nameof(hash), nameof(ComponentType.Swift));
    }

    public SwiftComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("commitHash")]
    public string CommitHash { get; set; }

    [JsonPropertyName("repositoryUrl")]
    public Uri RepositoryUrl { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Swift;

    // Example PackageUrl -> pkg:swift/github.com/apple/swift-asn1
    // type: swift
    // namespace: github.com/apple
    // name: swift-asn1
    [JsonPropertyName("packageUrl")]
    public override PackageURL PackageUrl => new PackageURL(
        type: "swift",
        @namespace: this.GetNamespaceFromPackageUrl(),
        name: this.Name,
        version: this.Version,
        qualifiers: new SortedDictionary<string, string>
        {
            { "repository_url", this.RepositoryUrl.AbsoluteUri },
        },
        subpath: null);

    protected override string ComputeBaseId() => $"{this.RepositoryUrl.AbsoluteUri} {this.CommitHash} - {this.Type}";

    protected override IEnumerable<KeyValuePair<string, string>> GetExtendedIdProperties()
    {
        yield return new KeyValuePair<string, string>(nameof(this.Name), this.Name);
        yield return new KeyValuePair<string, string>(nameof(this.Version), this.Version);
    }

    private string GetNamespaceFromPackageUrl()
    {
        // In the case of github.com, the namespace should contain the user/organization
        // See https://github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst#swift
        var uppercaseHost = this.RepositoryUrl.Host.ToUpperInvariant();
        if (uppercaseHost.Contains("GITHUB.COM"))
        {
            // The first segment of the URL will contain the user or organization for GitHub
            var firstSegment = this.RepositoryUrl.Segments[1].Trim('/');
            return $"{this.RepositoryUrl.Host}/{firstSegment}";
        }

        // In the default case of a generic host, the namespace should be the just the host
        return this.RepositoryUrl.Host;
    }
}
