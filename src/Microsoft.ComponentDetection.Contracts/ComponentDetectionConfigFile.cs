namespace Microsoft.ComponentDetection.Contracts;

using System.Collections.Generic;
using YamlDotNet.Serialization;

/// <summary>
/// Represents the ComponentDetection.yml config file.
/// </summary>
public class ComponentDetectionConfigFile
{
    /// <summary>
    /// Gets or sets a value indicating whether the detection should be stopped.
    /// </summary>
    [YamlMember(Alias = "variables")]
    public Dictionary<string, string> Variables { get; set; } = [];
}
