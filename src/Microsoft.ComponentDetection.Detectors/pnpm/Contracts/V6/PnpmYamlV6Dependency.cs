#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using YamlDotNet.Serialization;

public class PnpmYamlV6Dependency
{
    [YamlMember(Alias = "version")]
    public string Version { get; set; }
}
