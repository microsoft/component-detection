namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using System.Collections.Generic;
using YamlDotNet.Serialization;

public sealed record YarnBerryLockfileEntry
{
    /// <summary>
    /// The version of the package.
    /// </summary>
    [YamlMember(Alias = "version")]
    public string Version { get; init; }

    [YamlMember(Alias = "resolution")]
    public string Resolution { get; init; }

    [YamlMember(Alias = "dependencies")]
    public IDictionary<string, string> Dependencies { get; init; }

    [YamlMember(Alias = "peerDependencies")]
    public IDictionary<string, string> PeerDependencies { get; init; }

    [YamlMember(Alias = "peerDependenciesMeta")]
    public IDictionary<string, YarnBerryPeerDependencyMeta> PeerDependenciesMeta { get; init; }

    [YamlMember(Alias = "dependenciesMeta")]
    public IDictionary<string, YarnBerryDependencyMeta> DependenciesMeta { get; init; }

    [YamlMember(Alias = "bin")]
    public string Bin { get; init; }

    [YamlMember(Alias = "linkType")]
    public string LinkType { get; init; }

    [YamlMember(Alias = "languageName")]
    public string LanguageName { get; init; }

    [YamlMember(Alias = "checksum")]
    public string Checksum { get; init; }

    [YamlMember(Alias = "conditions")]
    public string Conditions { get; init; }
}
