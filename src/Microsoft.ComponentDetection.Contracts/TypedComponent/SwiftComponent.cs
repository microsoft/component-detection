#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using PackageUrl;

/// <summary>
/// Represents a Swift package manager component.
/// </summary>
public class SwiftComponent : TypedComponent
{
    private readonly Uri packageUrl;

    private readonly string hash;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftComponent"/> class.
    /// </summary>
    /// <param name="name">The name of the component.</param>
    /// <param name="version">The version of the component.</param>
    /// <param name="packageUrl">The package URL of the component.</param>
    /// <param name="hash">The hash of the component.</param>
    public SwiftComponent(string name, string version, string packageUrl, string hash)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(name), nameof(ComponentType.Swift));
        this.Version = this.ValidateRequiredInput(version, nameof(version), nameof(ComponentType.Swift));
        this.ValidateRequiredInput(packageUrl, nameof(packageUrl), nameof(ComponentType.Swift));
        this.packageUrl = new Uri(packageUrl);
        this.hash = this.ValidateRequiredInput(hash, nameof(hash), nameof(ComponentType.Swift));
    }

    public string Name { get; }

    public string Version { get; }

    public override ComponentType Type => ComponentType.Swift;

    // Example PackageURL -> pkg:swift/github.com/apple/swift-asn1
    // type: swift
    // namespace: github.com/apple
    // name: swift-asn1
    public PackageURL PackageURL => new PackageURL(
        type: "swift",
        @namespace: this.GetNamespaceFromPackageUrl(),
        name: this.Name,
        version: this.Version,
        qualifiers: new SortedDictionary<string, string>
        {
            { "repository_url", this.packageUrl.AbsoluteUri },
        },
        subpath: null);

    protected override string ComputeId() => $"{this.Name} {this.Version} - {this.Type}";

    private string GetNamespaceFromPackageUrl()
    {
        // In the case of github.com, the namespace should contain the user/organization
        // See https://github.com/package-url/purl-spec/blob/master/PURL-TYPES.rst#swift
        var uppercaseHost = this.packageUrl.Host.ToUpperInvariant();
        if (uppercaseHost.Contains("GITHUB.COM"))
        {
            // The first segment of the URL will contain the user or organization for GitHub
            var firstSegment = this.packageUrl.Segments[1].Trim('/');
            return $"{this.packageUrl.Host}/{firstSegment}";
        }

        // In the default case of a generic host, the namespace should be the just the host
        return this.packageUrl.Host;
    }
}
