#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using Microsoft.ComponentDetection.Contracts;

/// <summary>
/// Interface that represents a version of the pnpm detector.
/// </summary>
public interface IPnpmDetector
{
    /// <summary>
    /// Parses a yaml file content in pnmp format into the dependecy graph.
    /// </summary>
    /// <param name="yamlFileContent">Content of the yaml file that contains the pnpm dependencies.</param>
    /// <param name="singleFileComponentRecorder">Component recorder to which to write the dependency graph.</param>
    public void RecordDependencyGraphFromFile(string yamlFileContent, ISingleFileComponentRecorder singleFileComponentRecorder);
}

/// <summary>
/// Constants used in Pnpm Detectors.
/// </summary>
public static class PnpmConstants
{
    public const string PnpmFileDependencyPath = "file:";

    public const string PnpmLinkDependencyPath = "link:";
    public const string PnpmHttpDependencyPath = "http:";
    public const string PnpmHttpsDependencyPath = "https:";
}
