namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

public partial class FrameworkPackages
{
    internal static NuGetFramework NetStandard20 { get; } = NuGetFramework.Parse("netstandard2.0");

    /// <summary>
    /// Packages provided by the .NET Standard 2.0 framework.
    /// NETStandard2.0 did not drop packages with PackageOverrides,
    /// instead it only dropped reference assemblies with conflict resolution via assembly comparison.
    /// This means that a user could still get these packages in their output if the force the
    /// NETStandard2.0 project to copy to the output directory by forcing with CopyLocalLockFileAssemblies.
    ///
    /// This list is valuable useful since all frameworks that support .NETStandard 2.0 will provide these
    /// packages.
    /// </summary>
    internal static FrameworkPackages NetStandard20Packages { get; } = new(NetStandard20)
    {
        { "Microsoft.Win32.Primitives", "4.3.0" },
        { "System.AppContext", "4.3.0" },
        { "System.Collections", "4.3.0" },
        { "System.Collections.Concurrent", "4.3.0" },
        { "System.Collections.NonGeneric", "4.3.0" },
        { "System.Collections.Specialized", "4.3.0" },
        { "System.ComponentModel", "4.3.0" },
        { "System.ComponentModel.EventBasedAsync", "4.3.0" },
        { "System.ComponentModel.Primitives", "4.3.0" },
        { "System.ComponentModel.TypeConverter", "4.3.0" },
        { "System.Console", "4.3.0" },
        { "System.Data.Common", "4.3.0" },
        { "System.Diagnostics.Contracts", "4.3.0" },
        { "System.Diagnostics.Debug", "4.3.0" },
        { "System.Diagnostics.FileVersionInfo", "4.3.0" },
        { "System.Diagnostics.Process", "4.3.0" },
        { "System.Diagnostics.StackTrace", "4.3.0" },
        { "System.Diagnostics.TextWriterTraceListener", "4.3.0" },
        { "System.Diagnostics.Tools", "4.3.0" },
        { "System.Diagnostics.TraceSource", "4.3.0" },
        { "System.Diagnostics.Tracing", "4.3.0" },
        { "System.Dynamic.Runtime", "4.3.0" },
        { "System.Globalization", "4.3.0" },
        { "System.Globalization.Calendars", "4.3.0" },
        { "System.Globalization.Extensions", "4.3.0" },
        { "System.IO", "4.3.0" },
        { "System.IO.Compression", "4.3.0" },
        { "System.IO.Compression.ZipFile", "4.3.0" },
        { "System.IO.FileSystem", "4.3.0" },
        { "System.IO.FileSystem.DriveInfo", "4.3.0" },
        { "System.IO.FileSystem.Primitives", "4.3.0" },
        { "System.IO.FileSystem.Watcher", "4.3.0" },
        { "System.IO.IsolatedStorage", "4.3.0" },
        { "System.IO.MemoryMappedFiles", "4.3.0" },
        { "System.IO.Pipes", "4.3.0" },
        { "System.IO.UnmanagedMemoryStream", "4.3.0" },
        { "System.Linq", "4.3.0" },
        { "System.Linq.Expressions", "4.3.0" },
        { "System.Linq.Queryable", "4.3.0" },
        { "System.Net.Http", "4.3.0" },
        { "System.Net.NameResolution", "4.3.0" },
        { "System.Net.Primitives", "4.3.0" },
        { "System.Net.Requests", "4.3.0" },
        { "System.Net.Security", "4.3.0" },
        { "System.Net.Sockets", "4.3.0" },
        { "System.Net.WebHeaderCollection", "4.3.0" },
        { "System.ObjectModel", "4.3.0" },
        { "System.Private.DataContractSerialization", "4.3.0" },
        { "System.Reflection", "4.3.0" },
        { "System.Reflection.Extensions", "4.3.0" },
        { "System.Reflection.Primitives", "4.3.0" },
        { "System.Reflection.TypeExtensions", "4.3.0" },
        { "System.Resources.ResourceManager", "4.3.0" },
        { "System.Runtime", "4.3.0" },
        { "System.Runtime.Extensions", "4.3.0" },
        { "System.Runtime.Handles", "4.3.0" },
        { "System.Runtime.InteropServices", "4.3.0" },
        { "System.Runtime.InteropServices.RuntimeInformation", "4.3.0" },
        { "System.Runtime.Loader", "4.3.0" },
        { "System.Runtime.Numerics", "4.3.0" },
        { "System.Runtime.Serialization.Formatters", "4.3.0" },
        { "System.Runtime.Serialization.Json", "4.3.0" },
        { "System.Runtime.Serialization.Primitives", "4.3.0" },
        { "System.Security.AccessControl", "4.4.0" },
        { "System.Security.Claims", "4.3.0" },
        { "System.Security.Cryptography.Algorithms", "4.3.0" },
        { "System.Security.Cryptography.Csp", "4.3.0" },
        { "System.Security.Cryptography.Encoding", "4.3.0" },
        { "System.Security.Cryptography.Primitives", "4.3.0" },
        { "System.Security.Cryptography.X509Certificates", "4.3.0" },
        { "System.Security.Cryptography.Xml", "4.4.0" },
        { "System.Security.Principal", "4.3.0" },
        { "System.Security.Principal.Windows", "4.3.0" },
        { "System.Text.Encoding", "4.3.0" },
        { "System.Text.Encoding.Extensions", "4.3.0" },
        { "System.Text.RegularExpressions", "4.3.0" },
        { "System.Threading", "4.3.0" },
        { "System.Threading.Overlapped", "4.3.0" },
        { "System.Threading.Tasks", "4.3.0" },
        { "System.Threading.Tasks.Parallel", "4.3.0" },
        { "System.Threading.Thread", "4.3.0" },
        { "System.Threading.ThreadPool", "4.3.0" },
        { "System.Threading.Timer", "4.3.0" },
        { "System.ValueTuple", "4.3.0" },
        { "System.Xml.ReaderWriter", "4.3.0" },
        { "System.Xml.XDocument", "4.3.0" },
        { "System.Xml.XmlDocument", "4.3.0" },
        { "System.Xml.XmlSerializer", "4.3.0" },
        { "System.Xml.XPath", "4.3.0" },
        { "System.Xml.XPath.XDocument", "4.3.0" },
        { "System.Xml.XPath.XmlDocument", "4.3.0" }
    };

}
