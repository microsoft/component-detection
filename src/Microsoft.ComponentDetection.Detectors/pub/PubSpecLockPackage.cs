namespace Microsoft.ComponentDetection.Detectors.Pub;

using System.Runtime.Serialization;
using YamlDotNet.Serialization;

/// <summary>
/// Model of the pub-spec lock file.
/// </summary>
[DataContract]
public class PubSpecLockPackage
{
    [YamlMember(Alias = "source")]
    public string Source { get; set; }

    [YamlMember(Alias = "version")]
    public string Version { get; set; }

    [YamlMember(Alias = "dependency")]
    public string Dependency { get; set; }

    [YamlMember(Alias = "description")]
    public dynamic Description { get; set; }

    /// <summary>
    /// /// Returns the description\sha256 path
    /// The value can be null.
    /// </summary>
    /// <returns> Returns the package SHA-256 as in the pubspec.lock file.</returns>
    public string GetSha256() => this.Description["sha256"];

    /// <summary>
    /// Returns the description\url path
    /// The value can be null.
    /// </summary>
    /// <returns>Returns the package url as in the pubspec.lock file.</returns>
    public string GePackageDownloadedSource() => this.Description["url"];

    /// <summary>
    /// Returns the description\name path
    /// The value can be null.
    /// </summary>
    /// <returns>Returns the package name as in the pubspec.lock file.</returns>
    public string GetName() => this.Description["name"];
}
