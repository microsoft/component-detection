#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

/// <summary>Class of detector, the names of which are converted into categories for all default detectors.</summary>
public enum DetectorClass
{
    /// <summary>Default value, which indicates all classes should be run. Not used as an actual category.</summary>
    All,

    /// <summary>Indicates a detector applies to NPM packages.</summary>
    Npm,

    /// <summary>Indicates a detector applies to NuGet packages.</summary>
    NuGet,

    /// <summary>Indicates a detector applies to Maven packages.</summary>
    Maven,

    /// <summary>Indicates a detector applies to RubyGems packages.</summary>
    RubyGems,

    /// <summary>Indicates a detector applies to Cargo packages.</summary>
    Cargo,

    /// <summary>Indicates a detector applies to Pip packages.</summary>
    Pip,

    /// <summary>Indicates a detector applies to Go modules.</summary>
    GoMod,

    /// <summary>Indicates a detector applies to CocoaPods packages.</summary>
    CocoaPods,

    /// <summary>Indicates a detector applies to Linux packages.</summary>
    Linux,

    /// <summary>Indicates a detector applies to Conda packages.</summary>
    Conda,

    /// <summary>Indicates a detector applies to SPDX files.</summary>
    Spdx,

    /// <summary>Indicates a detector applies to Vcpkg packages.</summary>
    Vcpkg,

    /// <summary>Indicates a detector applies to Docker references.</summary>
    DockerReference,

    /// <summary> Indicates a detector applies to Swift packages.</summary>
    Swift,
}
