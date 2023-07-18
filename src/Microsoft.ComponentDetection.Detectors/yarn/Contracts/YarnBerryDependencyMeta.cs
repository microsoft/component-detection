namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using YamlDotNet.Serialization;

public sealed record YarnBerryDependencyMeta
{
    [YamlMember(Alias = "built")]
    public bool Built { get; init; }

    [YamlMember(Alias = "optional")]
    public bool Optional { get; init; }

    [YamlMember(Alias = "unplugged")]
    public bool Unplugged { get; init; }
}
