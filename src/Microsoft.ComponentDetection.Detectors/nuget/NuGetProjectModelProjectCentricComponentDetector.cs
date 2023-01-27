namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using global::NuGet.Packaging.Core;
using global::NuGet.ProjectModel;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;

[Export(typeof(IComponentDetector))]
public class NuGetProjectModelProjectCentricComponentDetector : FileComponentDetector
{
    public const string OmittedFrameworkComponentsTelemetryKey = "OmittedFrameworkComponents";

    public const string ProjectDependencyType = "project";

    private readonly ConcurrentDictionary<string, int> frameworkComponentsThatWereOmmittedWithCount = new ConcurrentDictionary<string, int>();

    private readonly List<string> netCoreFrameworkNames = new List<string> { "Microsoft.AspNetCore.App", "Microsoft.AspNetCore.Razor.Design", "Microsoft.NETCore.App" };

    private readonly HashSet<string> alreadyLoggedWarnings = new HashSet<string>();

    // This list is meant to encompass all net standard dependencies, but likely contains some net core app 1.x ones, too.
    // The specific guidance we got around populating this list is to do so based on creating a dotnet core 1.x app to make sure we had the complete
    //  set of netstandard.library files that could show up in later sdk versions.
    private readonly string[] netStandardDependencies = new[]
    {
        "Libuv",
        "Microsoft.CodeAnalysis.Analyzers",
        "Microsoft.CodeAnalysis.Common",
        "Microsoft.CodeAnalysis.CSharp",
        "Microsoft.CodeAnalysis.VisualBasic",
        "Microsoft.CSharp",
        "Microsoft.DiaSymReader.Native",
        "Microsoft.NETCore.DotNetHost",
        "Microsoft.NETCore.DotNetHostPolicy",
        "Microsoft.NETCore.DotNetHostResolver",
        "Microsoft.NETCore.Jit",
        "Microsoft.NETCore.Platforms",
        "Microsoft.NETCore.Runtime.CoreCLR",
        "Microsoft.NETCore.Targets",
        "Microsoft.NETCore.Windows.ApiSets",
        "Microsoft.VisualBasic",
        "Microsoft.Win32.Primitives",
        "Microsoft.Win32.Registry",
        "NETStandard.Library",
        "runtime.debian.8-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.fedora.23-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.fedora.24-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.native.System",
        "runtime.native.System.IO.Compression",
        "runtime.native.System.Net.Http",
        "runtime.native.System.Net.Security",
        "runtime.native.System.Security.Cryptography.Apple",
        "runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.opensuse.13.2-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.opensuse.42.1-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.osx.10.10-x64.runtime.native.System.Security.Cryptography.Apple",
        "runtime.osx.10.10-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.rhel.7-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.ubuntu.14.04-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.ubuntu.16.04-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "runtime.ubuntu.16.10-x64.runtime.native.System.Security.Cryptography.OpenSsl",
        "System.AppContext",
        "System.Buffers",
        "System.Collections",
        "System.Collections.Concurrent",
        "System.Collections.Immutable",
        "System.Collections.NonGeneric",
        "System.Collections.Specialized",
        "System.ComponentModel",
        "System.ComponentModel.Annotations",
        "System.ComponentModel.EventBasedAsync",
        "System.ComponentModel.Primitives",
        "System.ComponentModel.TypeConverter",
        "System.Console",
        "System.Data.Common",
        "System.Diagnostics.Contracts",
        "System.Diagnostics.Debug",
        "System.Diagnostics.DiagnosticSource",
        "System.Diagnostics.FileVersionInfo",
        "System.Diagnostics.Process",
        "System.Diagnostics.StackTrace",
        "System.Diagnostics.TextWriterTraceListener",
        "System.Diagnostics.Tools",
        "System.Diagnostics.TraceSource",
        "System.Diagnostics.Tracing",
        "System.Drawing.Primitives",
        "System.Dynamic.Runtime",
        "System.Globalization",
        "System.Globalization.Calendars",
        "System.Globalization.Extensions",
        "System.IO",
        "System.IO.Compression",
        "System.IO.Compression.ZipFile",
        "System.IO.FileSystem",
        "System.IO.FileSystem.DriveInfo",
        "System.IO.FileSystem.Primitives",
        "System.IO.FileSystem.Watcher",
        "System.IO.IsolatedStorage",
        "System.IO.MemoryMappedFiles",
        "System.IO.Pipes",
        "System.IO.UnmanagedMemoryStream",
        "System.Linq",
        "System.Linq.Expressions",
        "System.Linq.Parallel",
        "System.Linq.Queryable",
        "System.Net.Http",
        "System.Net.HttpListener",
        "System.Net.Mail",
        "System.Net.NameResolution",
        "System.Net.NetworkInformation",
        "System.Net.Ping",
        "System.Net.Primitives",
        "System.Net.Requests",
        "System.Net.Security",
        "System.Net.ServicePoint",
        "System.Net.Sockets",
        "System.Net.WebClient",
        "System.Net.WebHeaderCollection",
        "System.Net.WebProxy",
        "System.Net.WebSockets",
        "System.Net.WebSockets.Client",
        "System.Numerics.Vectors",
        "System.ObjectModel",
        "System.Reflection",
        "System.Reflection.DispatchProxy",
        "System.Reflection.Emit",
        "System.Reflection.Emit.ILGeneration",
        "System.Reflection.Emit.Lightweight",
        "System.Reflection.Extensions",
        "System.Reflection.Metadata",
        "System.Reflection.Primitives",
        "System.Reflection.TypeExtensions",
        "System.Resources.Reader",
        "System.Resources.ResourceManager",
        "System.Resources.Writer",
        "System.Runtime",
        "System.Runtime.CompilerServices.VisualC",
        "System.Runtime.Extensions",
        "System.Runtime.Handles",
        "System.Runtime.InteropServices",
        "System.Runtime.InteropServices.RuntimeInformation",
        "System.Runtime.Loader",
        "System.Runtime.Numerics",
        "System.Runtime.Serialization.Formatters",
        "System.Runtime.Serialization.Json",
        "System.Runtime.Serialization.Primitives",
        "System.Runtime.Serialization.Xml",
        "System.Security.Claims",
        "System.Security.Cryptography.Algorithms",
        "System.Security.Cryptography.Cng",
        "System.Security.Cryptography.Csp",
        "System.Security.Cryptography.Encoding",
        "System.Security.Cryptography.OpenSsl",
        "System.Security.Cryptography.Primitives",
        "System.Security.Cryptography.X509Certificates",
        "System.Security.Principal",
        "System.Security.Principal.Windows",
        "System.Text.Encoding",
        "System.Text.Encoding.CodePages",
        "System.Text.Encoding.Extensions",
        "System.Text.RegularExpressions",
        "System.Threading",
        "System.Threading.Overlapped",
        "System.Threading.Tasks",
        "System.Threading.Tasks.Dataflow",
        "System.Threading.Tasks.Extensions",
        "System.Threading.Tasks.Parallel",
        "System.Threading.Thread",
        "System.Threading.ThreadPool",
        "System.Threading.Timer",
        "System.Web.HttpUtility",
        "System.Xml.ReaderWriter",
        "System.Xml.XDocument",
        "System.Xml.XmlDocument",
        "System.Xml.XmlSerializer",
        "System.Xml.XPath",
        "System.Xml.XPath.XDocument",
    };

    public override string Id { get; } = "NuGetProjectCentric";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "project.assets.json" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.NuGet };

    public override int Version { get; } = 1;

    [Import]
    public IFileUtilityService FileUtilityService { get; set; }

    protected override Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        try
        {
            var lockFile = new LockFileFormat().Read(processRequest.ComponentStream.Stream, processRequest.ComponentStream.Location);

            if (lockFile.PackageSpec == null)
            {
                throw new FormatException("Lockfile did not contain a PackageSpec");
            }

            var frameworkComponents = this.GetFrameworkComponents(lockFile);
            var explicitReferencedDependencies = this.GetTopLevelLibraries(lockFile)
                .Select(x => this.GetLibraryComponentWithDependencyLookup(lockFile.Libraries, x.Name, x.Version, x.VersionRange))
                .ToList();
            var explicitlyReferencedComponentIds =
                explicitReferencedDependencies
                    .Select(x => new NuGetComponent(x.Name, x.Version.ToNormalizedString()).Id)
                    .ToHashSet();

            // Since we report projects as the location, we ignore the passed in single file recorder.
            var singleFileComponentRecorder = this.ComponentRecorder.CreateSingleFileComponentRecorder(lockFile.PackageSpec.RestoreMetadata.ProjectPath);
            foreach (var target in lockFile.Targets)
            {
                // This call to GetTargetLibrary is not guarded, because if this can't be resolved then something is fundamentally broken (e.g. an explicit dependency reference not being in the list of libraries)
                foreach (var library in explicitReferencedDependencies.Select(x => target.GetTargetLibrary(x.Name)).Where(x => x != null))
                {
                    this.NavigateAndRegister(target, explicitlyReferencedComponentIds, singleFileComponentRecorder, library, null, frameworkComponents);
                }
            }
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the package
            this.Logger.LogFailedReadingFile(processRequest.ComponentStream.Location, e);
        }

        return Task.CompletedTask;
    }

    protected override Task OnDetectionFinished()
    {
        this.Telemetry.Add(OmittedFrameworkComponentsTelemetryKey, JsonConvert.SerializeObject(this.frameworkComponentsThatWereOmmittedWithCount));

        return Task.CompletedTask;
    }

    private void NavigateAndRegister(
        LockFileTarget target,
        HashSet<string> explicitlyReferencedComponentIds,
        ISingleFileComponentRecorder singleFileComponentRecorder,
        LockFileTargetLibrary library,
        string parentComponentId,
        HashSet<string> dotnetRuntimePackageNames,
        HashSet<string> visited = null)
    {
        if (this.IsAFrameworkComponent(dotnetRuntimePackageNames, library.Name, library.Dependencies)
            || library.Type == ProjectDependencyType)
        {
            return;
        }

        visited ??= new HashSet<string>();

        var libraryComponent = new DetectedComponent(new NuGetComponent(library.Name, library.Version.ToNormalizedString()));
        singleFileComponentRecorder.RegisterUsage(libraryComponent, explicitlyReferencedComponentIds.Contains(libraryComponent.Component.Id), parentComponentId);

        foreach (var dependency in library.Dependencies)
        {
            if (visited.Contains(dependency.Id))
            {
                continue;
            }

            var targetLibrary = target.GetTargetLibrary(dependency.Id);
            if (targetLibrary == null)
            {
                // We have to exclude this case -- it looks like a bug in project.assets.json, but there are project.assets.json files that don't have a dependency library in the libraries set.
            }
            else
            {
                visited.Add(dependency.Id);
                this.NavigateAndRegister(target, explicitlyReferencedComponentIds, singleFileComponentRecorder, targetLibrary, libraryComponent.Component.Id, dotnetRuntimePackageNames, visited);
            }
        }
    }

    private bool IsAFrameworkComponent(HashSet<string> frameworkComponents, string libraryName, IList<PackageDependency> dependencies = null)
    {
        var isAFrameworkComponent = frameworkComponents.Contains(libraryName);

        if (isAFrameworkComponent)
        {
            this.frameworkComponentsThatWereOmmittedWithCount.AddOrUpdate(libraryName, 1, (name, existing) => existing + 1);

            if (dependencies != null)
            {
                // Also track shallow children if this is a top level library so we have a rough count of how many things have been ommitted + root relationships
                foreach (var item in dependencies)
                {
                    this.frameworkComponentsThatWereOmmittedWithCount.AddOrUpdate(item.Id, 1, (name, existing) => existing + 1);
                }
            }
        }

        return isAFrameworkComponent;
    }

    private List<(string Name, Version Version, VersionRange VersionRange)> GetTopLevelLibraries(LockFile lockFile)
    {
        // First, populate target frameworks -- This is the base level authoritative list of nuget packages a project has dependencies on.
        var toBeFilled = new List<(string Name, Version Version, VersionRange VersionRange)>();

        foreach (var framework in lockFile.PackageSpec.TargetFrameworks)
        {
            foreach (var dependency in framework.Dependencies)
            {
                toBeFilled.Add((dependency.Name, Version: null, dependency.LibraryRange.VersionRange));
            }
        }

        // Next, we need to resolve project references -- This is a little funky, because project references are only stored via path in
        //  project.assets.json, so we first build a list of all paths and then compare what is top level to them to resolve their
        //  associated library.
        var projectDirectory = Path.GetDirectoryName(lockFile.PackageSpec.RestoreMetadata.ProjectPath);
        var librariesWithAbsolutePath =
            lockFile.Libraries.Where(x => x.Type == ProjectDependencyType)
                .Select(x => (library: x, absoluteProjectPath: Path.GetFullPath(Path.Combine(projectDirectory, x.Path))))
                .ToDictionary(x => x.absoluteProjectPath, x => x.library);

        foreach (var restoreMetadataTargetFramework in lockFile.PackageSpec.RestoreMetadata.TargetFrameworks)
        {
            foreach (var projectReference in restoreMetadataTargetFramework.ProjectReferences)
            {
                if (librariesWithAbsolutePath.TryGetValue(Path.GetFullPath(projectReference.ProjectPath), out var library))
                {
                    toBeFilled.Add((library.Name, library.Version.Version, null));
                }
            }
        }

        return toBeFilled;
    }

    // Looks up a library in project.assets.json given a version (preferred) or version range (have to in some cases due to how project.assets.json stores things)
    private LockFileLibrary GetLibraryComponentWithDependencyLookup(IList<LockFileLibrary> libraries, string dependencyId, Version version, VersionRange versionRange)
    {
        if ((version == null && versionRange == null) || (version != null && versionRange != null))
        {
            throw new ArgumentException($"Either {nameof(version)} or {nameof(versionRange)} must be specified, but not both.");
        }

        var matchingLibraryNames = libraries.Where(x => string.Equals(x.Name, dependencyId, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matchingLibraryNames.Count == 0)
        {
            throw new InvalidOperationException("Project.assets.json is malformed, no library could be found matching: " + dependencyId);
        }

        LockFileLibrary matchingLibrary;
        if (version != null)
        {
            // .Version.Version ensures we get to a nuget normalized 4 part version
            matchingLibrary = matchingLibraryNames.FirstOrDefault(x => x.Version.Version.Equals(version));
        }
        else
        {
            matchingLibrary = matchingLibraryNames.FirstOrDefault(x => versionRange.Satisfies(x.Version));
        }

        if (matchingLibrary == null)
        {
            matchingLibrary = matchingLibraryNames.First();
            var logMessage = $"Couldn't satisfy lookup for {(versionRange != null ? versionRange.ToNormalizedString() : version.ToString())}. Falling back to first found component for {matchingLibrary.Name}, resolving to version {matchingLibrary.Version}.";
            if (!this.alreadyLoggedWarnings.Contains(logMessage))
            {
                this.Logger.LogWarning(logMessage);
                this.alreadyLoggedWarnings.Add(logMessage);
            }
        }

        return matchingLibrary;
    }

    private HashSet<string> GetFrameworkComponents(LockFile lockFile)
    {
        var frameworkDependencies = new HashSet<string>();
        foreach (var projectFileDependencyGroup in lockFile.ProjectFileDependencyGroups)
        {
            var topLevelLibraries = this.GetTopLevelLibraries(lockFile);
            foreach (var (name, version, versionRange) in topLevelLibraries)
            {
                if (this.netCoreFrameworkNames.Contains(name))
                {
                    frameworkDependencies.Add(name);

                    foreach (var target in lockFile.Targets)
                    {
                        var matchingLibrary = target.Libraries.FirstOrDefault(x => x.Name == name);
                        var dependencyComponents = this.GetDependencyComponentIds(lockFile, target, matchingLibrary.Dependencies);
                        frameworkDependencies.UnionWith(dependencyComponents);
                    }
                }
            }
        }

        foreach (var netstandardDep in this.netStandardDependencies)
        {
            frameworkDependencies.Add(netstandardDep);
        }

        return frameworkDependencies;
    }

    private HashSet<string> GetDependencyComponentIds(LockFile lockFile, LockFileTarget target, IList<PackageDependency> dependencies, HashSet<string> visited = null)
    {
        visited ??= new HashSet<string>();
        var currentComponents = new HashSet<string>();
        foreach (var dependency in dependencies)
        {
            if (visited.Contains(dependency.Id))
            {
                continue;
            }

            currentComponents.Add(dependency.Id);
            var libraryToExpand = target.GetTargetLibrary(dependency.Id);
            if (libraryToExpand == null)
            {
                // We have to exclude this case -- it looks like a bug in project.assets.json, but there are project.assets.json files that don't have a dependency library in the libraries set.
            }
            else
            {
                visited.Add(dependency.Id);
                currentComponents.UnionWith(this.GetDependencyComponentIds(lockFile, target, libraryToExpand.Dependencies, visited));
            }
        }

        return currentComponents;
    }
}
