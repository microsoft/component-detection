namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using PackageUrl;

/// <summary>
/// Represents a SwiftPM component.
/// </summary>
public class SwiftPMComponent : TypedComponent
{
    private readonly string packageUrl;

    private readonly string hash;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftPMComponent"/> class.
    /// </summary>
    /// <param name="name">The name of the component.</param>
    /// <param name="version">The version of the component.</param>
    /// <param name="packageUrl">The package URL of the component.</param>
    /// <param name="hash">The hash of the component.</param>
    public SwiftPMComponent(string name, string version, string packageUrl, string hash)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(name), nameof(ComponentType.SwiftPM));
        this.Version = this.ValidateRequiredInput(version, nameof(version), nameof(ComponentType.SwiftPM));
        this.packageUrl = this.ValidateRequiredInput(packageUrl, nameof(packageUrl), nameof(ComponentType.SwiftPM));
        this.hash = this.ValidateRequiredInput(hash, nameof(hash), nameof(ComponentType.SwiftPM));
    }

    public string Name { get; }

    public string Version { get; }

    public override ComponentType Type => ComponentType.SwiftPM;

    public override string Id => $"{this.Name} {this.Version} - {this.Type}";

    // The type is swiftpm
    public PackageURL PackageURL => new PackageURL(
        type: "swift",
        @namespace: new Uri(this.packageUrl).Host,
        name: this.Name,
        version: this.hash, // Hash has priority over version when creating a PackageURL
        qualifiers: new SortedDictionary<string, string>
        {
            { "repository_url", this.packageUrl },
        },
        subpath: null);
}
