namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for .NETCoreApp,Version=v3.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETCoreApp30
    {
        internal static FrameworkPackages Instance { get; } = new(NuGetFramework.Parse("netcoreapp3.0"), NETCoreApp21.Instance)
        {
            { "Microsoft.CSharp", "4.4.0" },
            { "Microsoft.Win32.Registry", "4.4.0" },
            { "System.Collections.Immutable", "1.6.0" },
            { "System.ComponentModel.Annotations", "4.6.0" },
            { "System.Data.DataSetExtensions", "4.5.0" },
            { "System.Diagnostics.DiagnosticSource", "4.6.0" },
            { "System.IO.FileSystem.AccessControl", "4.4.0" },
            { "System.Numerics.Vectors", "4.5.0" },
            { "System.Private.DataContractSerialization", "4.3.0" },
            { "System.Reflection.DispatchProxy", "4.6.0" },
            { "System.Reflection.Metadata", "1.7.0" },
            { "System.Runtime.CompilerServices.Unsafe", "4.6.0" },
            { "System.Security.AccessControl", "4.4.0" },
            { "System.Security.Cryptography.Cng", "4.4.0" },
            { "System.Security.Cryptography.OpenSsl", "4.4.0" },
            { "System.Security.Cryptography.Xml", "4.4.0" },
            { "System.Security.Principal.Windows", "4.4.0" },
            { "System.Text.Encoding.CodePages", "4.6.0" },
            { "System.Text.Encodings.Web", "4.6.0" },
            { "System.Text.Json", "4.6.0" },
            { "System.Threading.Channels", "4.6.0" },
            { "System.Threading.Tasks.Dataflow", "4.10.0" },
        };
    }
}
