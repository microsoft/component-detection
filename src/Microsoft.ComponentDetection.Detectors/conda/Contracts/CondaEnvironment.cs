namespace Microsoft.ComponentDetection.Detectors.Poetry.Contracts;

using System.Collections.Generic;
using System.Runtime.Serialization;
using YamlDotNet.Serialization;

/// <summary>
/// Model of a conda environment yaml file.
/// </summary>
[DataContract]
public class CondaEnvironment
{
    [YamlMember(Alias = "dependencies")]
    public List<object> Dependencies { get; set; }
}
