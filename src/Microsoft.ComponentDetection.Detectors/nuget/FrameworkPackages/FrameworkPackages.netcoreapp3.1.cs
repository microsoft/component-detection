namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for .NETCoreApp,Version=v3.1.
/// </summary>
internal partial class FrameworkPackages
{
    internal static NuGetFramework NETCoreApp31 { get; } = NuGetFramework.Parse("netcoreapp3.1");

    internal static FrameworkPackages NETCoreApp31Packages { get; } = new(NETCoreApp31, NETCoreApp30Packages)
    {
        { "System.Collections.Immutable", "1.7.0" },
        { "System.ComponentModel.Annotations", "4.7.0" },
        { "System.Diagnostics.DiagnosticSource", "4.7.0" },
        { "System.Reflection.DispatchProxy", "4.7.0" },
        { "System.Reflection.Metadata", "1.8.0" },
        { "System.Runtime.CompilerServices.Unsafe", "4.7.1" },
        { "System.Text.Encoding.CodePages", "4.7.0" },
        { "System.Text.Encodings.Web", "4.7.0" },
        { "System.Text.Json", "4.7.0" },
        { "System.Threading.Channels", "4.7.0" },
        { "System.Threading.Tasks.Dataflow", "4.11.0" },
    };
}
