namespace Microsoft.ComponentDetection.Detectors.Uv;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Tomlyn;

[DataContract]
internal class UvLock
{
    // A list of packages with their dependencies.
    [DataMember(Name = "package")]
    public List<UvPackage> Packages { get; set; } = [];

    public static UvLock Parse(Stream tomlStream)
    {
        using var reader = new StreamReader(tomlStream);
        var tomlContent = reader.ReadToEnd();
        var options = new TomlModelOptions
        {
            IgnoreMissingProperties = true,
        };

        var parsed = Toml.ToModel<UvLock>(tomlContent, options: options);
        parsed.Normalize();
        return parsed;
    }

    private void Normalize()
    {
        this.Packages = this.Packages
            .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.Version))
            .ToList();

        foreach (var package in this.Packages)
        {
            package.Normalize();
        }
    }
}
