namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for net7.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETCoreApp70
    {
        internal static FrameworkPackages Instance { get; } = new(NuGetFramework.Parse("net7.0"), NETCoreApp60.Instance)
        {
            { "System.Collections.Immutable", "7.0.0" },
            { "System.Diagnostics.DiagnosticSource", "7.0.2" },
            { "System.Formats.Asn1", "7.0.0" },
            { "System.Net.Http.Json", "7.0.1" },
            { "System.Reflection.Metadata", "7.0.2" },
            { "System.Text.Encoding.CodePages", "7.0.0" },
            { "System.Text.Encodings.Web", "7.0.0" },
            { "System.Text.Json", "7.0.4" },
            { "System.Threading.Channels", "7.0.0" },
            { "System.Threading.Tasks.Dataflow", "7.0.0" },
        };
    }
}
