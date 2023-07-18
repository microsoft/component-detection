namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using YamlDotNet.Serialization;

public sealed record YarnBerryPeerDependencyMeta
{
    [YamlMember(Alias = "optional")]
    public bool Optional { get; init; }
}
