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
