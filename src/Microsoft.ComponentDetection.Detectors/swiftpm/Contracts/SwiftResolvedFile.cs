#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a Swift Package Manager component.
/// </summary>
public class SwiftResolvedFile
{
    [JsonPropertyName("pins")]
    public IList<SwiftDependency> Pins { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    public class SwiftDependency
    {
        // The name of the package
        [JsonPropertyName("identity")]
        public string Identity { get; set; }

        // How the package is imported. Example: "remoteSourceControl"
        // This is not an enum because the Swift contract does not specify the possible values.
        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        // The unique path to the repository where the package is located. Example: Git repo URL.
        [JsonPropertyName("location")]
        public string Location { get; set; }

        // Data about the package version and commit hash.
        [JsonPropertyName("state")]
        public SwiftState State { get; set; }

        public class SwiftState
        {
            // The commit hash of the package.
            [JsonPropertyName("revision")]
            public string Revision { get; set; }

            // The version of the package. Might be missing.
            [JsonPropertyName("version")]
            public string Version { get; set; }

            // The branch of the package. Might be missing.
            [JsonPropertyName("branch")]
            public string Branch { get; set; }
        }
    }
}
