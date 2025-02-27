namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Text;

#nullable enable

using PackageUrl;

public class DotNetComponent : TypedComponent
{
    private DotNetComponent()
    {
        /* Reserved for deserialization */
    }

    public DotNetComponent(string? sdkVersion, string? targetFramework = null, string? projectType = null)
    {
        this.SdkVersion = sdkVersion;
        this.TargetFramework = targetFramework;
        this.ProjectType = projectType;  // application, library, or null
    }

    /// <summary>
    /// SDK Version detected, could be null if no global.json exists and no dotnet is on the path.
    /// </summary>
    public string? SdkVersion { get; set; }

    /// <summary>
    /// Target framework for this instance.  Null in the case of global.json.
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Project type: application, library.  Null in the case of global.json or if no project output could be discovered.
    /// </summary>
    public string? ProjectType { get; set; }

    public override ComponentType Type => ComponentType.DotNet;

    /// <summary>
    /// Provides an id like `dotnet {SdkVersion} - {TargetFramework} - {ProjectType}` where targetFramework and projectType are only present if not null.
    /// </summary>
    public override string Id
    {
        get
        {
            var builder = new StringBuilder($"dotnet {this.SdkVersion ?? "unknown"}");
            if (this.TargetFramework is not null)
            {
                builder.Append($" - {this.TargetFramework}");

                if (this.ProjectType is not null)
                {
                    builder.Append($" - {this.ProjectType}");
                }
            }

            return builder.ToString();
        }
    }

    // TODO - do we need to add a type to prul https://github.com/package-url/purl-spec/blob/main/PURL-TYPES.rst
    public override PackageURL PackageUrl => new PackageURL("generic", null, "dotnet-sdk", this.SdkVersion ?? "unknown", null, null);
}
