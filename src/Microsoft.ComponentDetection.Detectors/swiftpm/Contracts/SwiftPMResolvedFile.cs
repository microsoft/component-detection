namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Represents a SwiftPM component.
/// </summary>
public class SwiftPMResolvedFile
{
    [JsonProperty("pins")]
    public IList<SwiftPMDependency> Pins { get; set; }

    [JsonProperty("version")]
    public int Version { get; set; }

    public class SwiftPMDependency
    {
        // The name of the package
        [JsonProperty("identity")]
        public string Identity { get; set; }

        // How the package is imported. Example: "remoteSourceControl"
        // This is not an enum because the SwiftPM contract does not specify the possible values.
        [JsonProperty("kind")]
        public string Kind { get; set; }

        // The unique path to the repository where the package is located. Example: Git repo URL.
        [JsonProperty("location")]
        public string Location { get; set; }

        // Data about the package version and commit hash.
        [JsonProperty("state")]
        public SwiftPMState State { get; set; }

        public class SwiftPMState
        {
            // The commit hash of the package.
            [JsonProperty("revision")]
            public string Revision { get; set; }

            // The version of the package. Might be missing.
            [JsonProperty("version")]
            public string Version { get; set; }

            // The branch of the package. Might be missing.
            [JsonProperty("branch")]
            public string Branch { get; set; }
        }
    }
}
