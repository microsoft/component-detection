namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for net8.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETCoreApp80
    {
        internal static FrameworkPackages Instance { get; } = new(NuGetFramework.Parse("net8.0"), NETCoreApp70.Instance)
        {
            { "System.Collections.Immutable", "8.0.0" },
            { "System.Diagnostics.DiagnosticSource", "8.0.1" },
            { "System.Formats.Asn1", "8.0.1" },
            { "System.Net.Http.Json", "8.0.0" },
            { "System.Reflection.Metadata", "8.0.0" },
            { "System.Text.Encoding.CodePages", "8.0.0" },
            { "System.Text.Encodings.Web", "8.0.0" },
            { "System.Text.Json", "8.0.4" },
            { "System.Threading.Channels", "8.0.0" },
            { "System.Threading.Tasks.Dataflow", "8.0.1" },
        };
    }
}
