#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using YamlDotNet.Serialization;

/// <summary>
/// Base class for all Pnpm lockfiles. Used for parsing the lockfile version.
/// </summary>
public class PnpmYaml
{
    [YamlMember(Alias = "lockfileVersion")]
    public string LockfileVersion { get; set; }
}
