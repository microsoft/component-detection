namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using YamlDotNet.Serialization;

public class PnpmYamlVersion
{
    [YamlMember(Alias = "lockfileVersion")]
    public string LockfileVersion { get; set; }
}
