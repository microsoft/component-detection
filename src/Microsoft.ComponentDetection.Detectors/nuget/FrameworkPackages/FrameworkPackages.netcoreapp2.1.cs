namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for .NETCoreApp,Version=v2.1.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETCoreApp21
    {
        internal static FrameworkPackages Instance { get; } = new(NuGetFramework.Parse("netcoreapp2.1"), "Microsoft.NETCore.App", NETCoreApp20.Instance)
        {
            { "Microsoft.CSharp", "4.5.0" },
            { "Microsoft.VisualBasic", "10.3.0" },
            { "Microsoft.Win32.Registry", "4.5.0" },
            { "System.Buffers", "4.5.0" },
            { "System.Collections.Immutable", "1.5.0" },
            { "System.ComponentModel.Annotations", "4.5.0" },
            { "System.Diagnostics.DiagnosticSource", "4.5.0" },
            { "System.IO.FileSystem.AccessControl", "4.5.0" },
            { "System.IO.Pipes.AccessControl", "4.5.0" },
            { "System.Memory", "4.5.5" },
            { "System.Numerics.Vectors", "4.5.0" },
            { "System.Reflection.DispatchProxy", "4.5.0" },
            { "System.Reflection.Metadata", "1.6.0" },
            { "System.Security.AccessControl", "4.5.0" },
            { "System.Security.Cryptography.Cng", "4.5.2" },
            { "System.Security.Cryptography.OpenSsl", "4.5.0" },
            { "System.Security.Principal.Windows", "4.5.0" },
            { "System.Threading.Tasks.Dataflow", "4.9.0" },
            { "System.Threading.Tasks.Extensions", "4.5.4" },
            { "System.ValueTuple", "4.5.0" },
        };

        internal static void Register() => FrameworkPackages.Register(Instance);
    }
}
