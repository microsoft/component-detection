namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for net5.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETCoreApp50
    {
        internal static FrameworkPackages Instance { get; } = new(NuGetFramework.Parse("net5.0"), NETCoreApp31.Instance)
        {
            { "Microsoft.CSharp", "4.7.0" },
            { "System.Buffers", "4.5.1" },
            { "System.Collections.Immutable", "5.0.0" },
            { "System.ComponentModel.Annotations", "5.0.0" },
            { "System.Diagnostics.DiagnosticSource", "5.0.0" },
            { "System.Formats.Asn1", "5.0.0" },
            { "System.Net.Http.Json", "5.0.0" },
            { "System.Reflection.DispatchProxy", "4.7.1" },
            { "System.Reflection.Metadata", "5.0.0" },
            { "System.Runtime.CompilerServices.Unsafe", "5.0.0" },
            { "System.Text.Encoding.CodePages", "5.0.0" },
            { "System.Text.Encodings.Web", "5.0.0" },
            { "System.Text.Json", "5.0.0" },
            { "System.Threading.Channels", "5.0.0" },
            { "System.Threading.Tasks.Dataflow", "5.0.0" },
        };
    }
}
