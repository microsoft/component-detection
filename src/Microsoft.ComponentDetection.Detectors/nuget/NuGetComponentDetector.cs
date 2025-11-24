#nullable disable
namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class NuGetComponentDetector : FileComponentDetector
{
    private static readonly IEnumerable<string> LowConfidencePackages = ["Newtonsoft.Json"];

    public const string NugetConfigFileName = "nuget.config";

    private readonly IList<string> repositoryPathKeyNames = ["repositorypath", "globalpackagesfolder"];

    public NuGetComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<NuGetComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id { get; } = "NuGet";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet)];

    public override IList<string> SearchPatterns { get; } = ["*.nupkg", "*.nuspec", NugetConfigFileName, "paket.lock"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.NuGet];

    public override int Version { get; } = 2;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var stream = processRequest.ComponentStream;
        var ignoreNugetConfig = detectorArgs.TryGetValue("NuGet.IncludeRepositoryPaths", out var includeRepositoryPathsValue) && includeRepositoryPathsValue.Equals(bool.FalseString, StringComparison.OrdinalIgnoreCase);

        if (NugetConfigFileName.Equals(stream.Pattern, StringComparison.OrdinalIgnoreCase))
        {
            await this.ProcessAdditionalDirectoryAsync(processRequest, ignoreNugetConfig);
        }
        else
        {
            await this.ProcessFileAsync(processRequest);
        }
    }

    private async Task ProcessAdditionalDirectoryAsync(ProcessRequest processRequest, bool ignoreNugetConfig)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var stream = processRequest.ComponentStream;

        if (!ignoreNugetConfig)
        {
            var additionalPaths = this.GetRepositoryPathsFromNugetConfig(stream);
            var rootPath = new Uri(this.CurrentScanRequest.SourceDirectory.FullName + Path.DirectorySeparatorChar);

            foreach (var additionalPath in additionalPaths)
            {
                // Only paths outside of our sourceDirectory need to be added
                if (!rootPath.IsBaseOf(new Uri(additionalPath.FullName + Path.DirectorySeparatorChar)))
                {
                    this.Logger.LogInformation("Found path override in nuget configuration file. Adding {NuGetAdditionalPath} to the package search path.", additionalPath);
                    this.Logger.LogWarning("Path {NuGetAdditionalPath} is not rooted in the source tree. More components may be detected than expected if this path is shared across code projects.", additionalPath);

                    this.Scanner.Initialize(additionalPath, (name, directoryName) => false, 1);

                    await this.Scanner.GetFilteredComponentStreamObservable(additionalPath, this.SearchPatterns.Where(sp => !NugetConfigFileName.Equals(sp)), singleFileComponentRecorder.GetParentComponentRecorder())
                        .ForEachAsync(async fi => await this.ProcessFileAsync(fi));
                }
            }
        }
    }

    private async Task ProcessFileAsync(ProcessRequest processRequest)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var stream = processRequest.ComponentStream;

        try
        {
            byte[] nuspecBytes = null;

            if ("*.nupkg".Equals(stream.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                nuspecBytes = await NuGetNuspecUtilities.GetNuspecBytesAsync(stream.Stream);
            }
            else if ("*.nuspec".Equals(stream.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                nuspecBytes = await NuGetNuspecUtilities.GetNuspecBytesFromNuspecStreamAsync(stream.Stream, stream.Stream.Length);
            }
            else if ("paket.lock".Equals(stream.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                this.ParsePaketLock(processRequest);
                return;
            }
            else
            {
                return;
            }

            using var nuspecStream = new MemoryStream(nuspecBytes, false);

            var doc = new XmlDocument();
            doc.Load(nuspecStream);

            XmlNode packageNode = doc["package"];
            XmlNode metadataNode = packageNode["metadata"];

            var name = metadataNode["id"]?.InnerText;
            var version = metadataNode["version"]?.InnerText;
            var authors = metadataNode["authors"]?.InnerText.Split(",").Select(author => author.Trim()).ToArray();

            if (name == null)
            {
                this.Logger.LogInformation("Could not parse name from Nuspec {NuspecLocation}", stream.Location);
                singleFileComponentRecorder.RegisterPackageParseFailure(stream.Location);

                return;
            }

            if (!NuGetVersion.TryParse(version, out var parsedVer))
            {
                this.Logger.LogInformation("Version '{NuspecVersion}' from {NuspecLocation} could not be parsed as a NuGet version", version, stream.Location);
                singleFileComponentRecorder.RegisterPackageParseFailure(stream.Location);

                return;
            }

            var component = new NuGetComponent(name, version, authors);
            if (!LowConfidencePackages.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
            }
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the component
            this.Logger.LogError(e, "Error parsing NuGet component from {NuspecLocation}", stream.Location);
            singleFileComponentRecorder.RegisterPackageParseFailure(stream.Location);
        }
    }

    private void ParsePaketLock(ProcessRequest processRequest)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var stream = processRequest.ComponentStream;

        using var reader = new StreamReader(stream.Stream);

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            var matches = Regex.Matches(line, @"\s*([a-zA-Z0-9-.]*) \([<>=]*[ ]*([0-9a-zA-Z-.]*)\)", RegexOptions.Singleline);
            foreach (var match in matches.Cast<Match>())
            {
                try
                {
                    var name = match.Groups[1].Value;
                    var version = match.Groups[2].Value;
                    var component = new NuGetComponent(name, version);
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
                }
                catch (Exception e)
                {
                    this.Logger.LogWarning(e, "Failed to parse paket.lock component from line `{Line}` in {Location}", line, stream.Location);
                    singleFileComponentRecorder.RegisterPackageParseFailure(stream.Location);
                }
            }
        }
    }

    private IList<DirectoryInfo> GetRepositoryPathsFromNugetConfig(IComponentStream componentStream)
    {
        var potentialPaths = new List<string>();
        var paths = new List<DirectoryInfo>();

        try
        {
            // Can be made async in later versions of .net standard.
            var root = XElement.Load(componentStream.Stream);

            var config = root.Elements().SingleOrDefault(x => x.Name == "config");

            if (config == null)
            {
                return paths;
            }

            foreach (var entry in config.Elements())
            {
                if (entry.Attributes().Any(x => this.repositoryPathKeyNames.Contains(x.Value.ToLower())))
                {
                    var value = entry.Attributes().SingleOrDefault(x => string.Equals(x.Name.LocalName, "value", StringComparison.OrdinalIgnoreCase))?.Value;

                    if (!string.IsNullOrEmpty(value))
                    {
                        potentialPaths.Add(value);
                    }
                }
            }
        }
        catch
        {
            // Eat all exceptions related to permissions or malformed XML
            return paths;
        }

        foreach (var potentialPath in potentialPaths)
        {
            DirectoryInfo path;

            if (Path.IsPathRooted(potentialPath))
            {
                path = new DirectoryInfo(Path.GetFullPath(potentialPath));
            }
            else if (this.IsValidPath(componentStream.Location))
            {
                path = new DirectoryInfo(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(componentStream.Location), potentialPath)));
            }
            else
            {
                this.Logger.LogWarning("Excluding discovered path {PotentialPath} from location {ComponentStreamLocation} as it could not be determined to be valid.", potentialPath, componentStream.Location);
                continue;
            }

            if (path.Exists)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>
    /// Checks to make sure a path is valid (does not have to exist).
    /// </summary>
    /// <param name="potentialPath"> The path to validate. </param>
    /// <returns>True if path is valid, otherwise it retuns false. </returns>
    private bool IsValidPath(string potentialPath)
    {
        if (potentialPath == null)
        {
            return false;
        }

        FileInfo fileInfo = null;

        try
        {
            fileInfo = new FileInfo(potentialPath);
        }
        catch
        {
            return false;
        }

        return fileInfo.Exists || fileInfo.Directory.Exists;
    }
}
