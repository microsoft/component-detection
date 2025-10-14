#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed record PipInstallationReportItem
{
    /// <summary>
    /// The metadata of the distribution.
    /// </summary>
    [JsonProperty("metadata")]
    public PipInstallationMetadata Metadata { get; set; }

    /// <summary>
    /// true if the requirement was provided as, or constrained to, a direct URL reference. false if the requirements was provided as a name and version specifier.
    /// </summary>
    [JsonProperty("is_direct")]
    public bool IsDirect { get; set; }

    /// <summary>
    /// true if the requirement was yanked from the index, but was still selected by pip conform.
    /// </summary>
    [JsonProperty("is_yanked")]
    public bool IsYanked { get; set; }

    /// <summary>
    /// true if the requirement was explicitly provided by the user, either directly via
    /// a command line argument or indirectly via a requirements file. false if the requirement
    /// was installed as a dependency of another requirement.
    /// </summary>
    [JsonProperty("requested")]
    public bool Requested { get; set; }

    /// <summary>
    /// See https://packaging.python.org/en/latest/specifications/direct-url-data-structure/.
    /// </summary>
    [JsonProperty("download_info")]
    public JObject DownloadInfo { get; set; }

    /// <summary>
    /// Extras requested by the user.
    /// </summary>
    [JsonProperty("requested_extras")]
    public JArray RequestedExtras { get; set; }
}
