#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using YamlDotNet.Serialization;

public class PnpmYamlV9Dependency
{
    [YamlMember(Alias = "version")]
    public string Version { get; set; }
}
