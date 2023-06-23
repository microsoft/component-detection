namespace Microsoft.ComponentDetection.Detectors.Poetry.Contracts;

using System.Collections.Generic;
using System.Runtime.Serialization;
using YamlDotNet.Serialization;

/// <summary>
/// Model of the conda-lock file.
/// </summary>
[DataContract]
public class CondaLock
{
    [YamlMember(Alias = "metadata")]
    public CondaMetadata Metadata { get; set; }

    [YamlMember(Alias = "package")]
    public List<CondaPackage> Package { get; set; }
}
