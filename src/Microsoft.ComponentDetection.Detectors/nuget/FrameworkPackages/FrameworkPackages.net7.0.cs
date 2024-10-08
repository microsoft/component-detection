namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for net7.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static NuGetFramework NETCoreApp70 { get; } = NuGetFramework.Parse("net7.0");

    internal static FrameworkPackages NETCoreApp70Packages { get; } = new(NETCoreApp70, NETCoreApp60Packages)
    {
        { "System.Collections.Immutable", "7.0.0" },
        { "System.Diagnostics.DiagnosticSource", "7.0.0" },
        { "System.Formats.Asn1", "7.0.0" },
        { "System.Net.Http.Json", "7.0.0" },
        { "System.Reflection.Metadata", "7.0.0" },
        { "System.Security.AccessControl", "6.0.1" },
        { "System.Text.Encoding.CodePages", "7.0.0" },
        { "System.Text.Encodings.Web", "7.0.0" },
        { "System.Text.Json", "7.0.0" },
        { "System.Threading.Channels", "7.0.0" },
        { "System.Threading.Tasks.Dataflow", "7.0.0" },
    };
}
