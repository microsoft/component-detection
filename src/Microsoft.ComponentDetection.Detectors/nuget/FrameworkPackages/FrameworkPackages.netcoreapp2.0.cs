namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for .NETCoreApp,Version=v2.0.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETCoreApp20
    {
        internal static FrameworkPackages Instance { get; } = new(NuGetFramework.Parse("netcoreapp2.0"), NETStandard20.Instance)
        {
        { "System.Buffers", "4.4.0" },
        { "System.Collections.Concurrent", "4.3.0" },
        { "System.Collections.Immutable", "1.4.0" },
        { "System.ComponentModel", "4.3.0" },
        { "System.ComponentModel.Annotations", "4.4.0" },
        { "System.ComponentModel.EventBasedAsync", "4.3.0" },
        { "System.Diagnostics.Contracts", "4.3.0" },
        { "System.Diagnostics.DiagnosticSource", "4.4.1" },
        { "System.Dynamic.Runtime", "4.3.0" },
        { "System.Linq.Parallel", "4.3.0" },
        { "System.Linq.Queryable", "4.3.0" },
        { "System.Net.Requests", "4.3.0" },
        { "System.Net.WebHeaderCollection", "4.3.0" },
        { "System.Numerics.Vectors", "4.4.0" },
        { "System.ObjectModel", "4.3.0" },
        { "System.Reflection.DispatchProxy", "4.4.0" },
        { "System.Reflection.Emit", "4.7.0" },
        { "System.Reflection.Emit.ILGeneration", "4.7.0" },
        { "System.Reflection.Emit.Lightweight", "4.7.0" },
        { "System.Reflection.Metadata", "1.5.0" },
        { "System.Reflection.TypeExtensions", "4.7.0" },
        { "System.Runtime.InteropServices.WindowsRuntime", "4.3.0" },
        { "System.Runtime.Loader", "4.3.0" },
        { "System.Runtime.Numerics", "4.3.0" },
        { "System.Runtime.Serialization.Json", "4.3.0" },
        { "System.Security.Principal", "4.3.0" },
        { "System.Text.RegularExpressions", "4.3.1" },
        { "System.Threading", "4.3.0" },
        { "System.Threading.Tasks.Dataflow", "4.8.0" },
        { "System.Threading.Tasks.Extensions", "4.4.0" },
        { "System.Threading.Tasks.Parallel", "4.3.0" },
        { "System.Xml.XDocument", "4.3.0" },
        { "System.Xml.XmlSerializer", "4.3.0" },
        };
    }
}
