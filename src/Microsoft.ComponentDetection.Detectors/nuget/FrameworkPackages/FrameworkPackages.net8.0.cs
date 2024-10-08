namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for net8.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static NuGetFramework NETCoreApp80 { get; } = NuGetFramework.Parse("net8.0");

    internal static FrameworkPackages NETCoreApp80Packages { get; } = new(NETCoreApp80, NETCoreApp70Packages)
    {
        { "System.Diagnostics.DiagnosticSource", "7.0.2" },
        { "System.Net.Http.Json", "7.0.1" },
        { "System.Reflection.Metadata", "7.0.2" },
        { "System.Text.Json", "7.0.4" },
    };
}
