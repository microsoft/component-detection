namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for net9.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETCoreApp90
    {
        internal static FrameworkPackages Instance { get; } = new(NuGetFramework.Parse("net9.0"), NETCoreApp80.Instance)
        {
            { "Microsoft.VisualBasic", "10.4.0" },
            { "System.Buffers", "5.0.0" },
            { "System.Collections.Immutable", "9.0.0" },
            { "System.Diagnostics.DiagnosticSource", "9.0.0" },
            { "System.Formats.Asn1", "9.0.0" },
            { "System.Formats.Tar", "9.0.0" },
            { "System.IO.Pipelines", "9.0.0" },
            { "System.Memory", "5.0.0" },
            { "System.Net.Http.Json", "9.0.0" },
            { "System.Numerics.Vectors", "5.0.0" },
            { "System.Private.Uri", "4.3.2" },
            { "System.Reflection.DispatchProxy", "6.0.0" },
            { "System.Reflection.Metadata", "9.0.0" },
            { "System.Runtime.CompilerServices.Unsafe", "7.0.0" },
            { "System.Text.Encoding.CodePages", "9.0.0" },
            { "System.Text.Encodings.Web", "9.0.0" },
            { "System.Text.Json", "9.0.0" },
            { "System.Threading.Channels", "9.0.0" },
            { "System.Threading.Tasks.Dataflow", "9.0.0" },
            { "System.Threading.Tasks.Extensions", "5.0.0" },
            { "System.Xml.XPath.XDocument", "5.0.0" },
        };
    }
}
