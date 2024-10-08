namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for net6.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static NuGetFramework NETCoreApp60 { get; } = NuGetFramework.Parse("net6.0");

    internal static FrameworkPackages NETCoreApp60Packages { get; } = new(NETCoreApp60, NETCoreApp50Packages)
    {
        { "Microsoft.Win32.Registry", "5.0.0" },
        { "System.Collections.Immutable", "6.0.0" },
        { "System.Diagnostics.DiagnosticSource", "6.0.1" },
        { "System.Formats.Asn1", "6.0.1" },
        { "System.IO.FileSystem.AccessControl", "5.0.0" },
        { "System.IO.Pipes.AccessControl", "5.0.0" },
        { "System.Net.Http.Json", "6.0.1" },
        { "System.Reflection.Metadata", "6.0.1" },
        { "System.Runtime.CompilerServices.Unsafe", "6.0.0" },
        { "System.Security.AccessControl", "6.0.1" },
        { "System.Security.Cryptography.Cng", "5.0.0" },
        { "System.Security.Cryptography.OpenSsl", "5.0.0" },
        { "System.Security.Principal.Windows", "5.0.0" },
        { "System.Text.Encoding.CodePages", "6.0.0" },
        { "System.Text.Encodings.Web", "6.0.0" },
        { "System.Text.Json", "6.0.9" },
        { "System.Threading.Channels", "6.0.0" },
        { "System.Threading.Tasks.Dataflow", "6.0.0" },
    };
}
