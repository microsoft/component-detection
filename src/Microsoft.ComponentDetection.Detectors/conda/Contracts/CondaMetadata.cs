#nullable disable
namespace Microsoft.ComponentDetection.Detectors.CondaLock.Contracts;

using System.Collections.Generic;
using YamlDotNet.Serialization;

/// <summary>
/// Model of the metadata section in the conda-lock file.
/// </summary>
public class CondaMetadata
{
    [YamlMember(Alias = "platforms")]
    public List<string> Platforms { get; set; }

    [YamlMember(Alias = "sources")]
    public List<string> Sources { get; set; }
}
