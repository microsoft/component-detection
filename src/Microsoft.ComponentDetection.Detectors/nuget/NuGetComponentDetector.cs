using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using NuGet.Versioning;

namespace Microsoft.ComponentDetection.Detectors.NuGet
{
    [Export(typeof(IComponentDetector))]
    public class NuGetComponentDetector : FileComponentDetector
    {
        private static readonly IEnumerable<string> LowConfidencePackages = new[] { "Newtonsoft.Json" };

        public override string Id { get; } = "NuGet";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.NuGet) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "*.nupkg", "*.nuspec", NugetConfigFileName, "paket.lock" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.NuGet };

        public override int Version { get; } = 2;

        public const string NugetConfigFileName = "nuget.config";

        private readonly IList<string> repositoryPathKeyNames = new List<string> { "repositorypath", "globalpackagesfolder" };

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var stream = processRequest.ComponentStream;
            var ignoreNugetConfig = detectorArgs.TryGetValue("NuGet.IncludeRepositoryPaths", out var includeRepositoryPathsValue) && includeRepositoryPathsValue.Equals(bool.FalseString, StringComparison.OrdinalIgnoreCase);

            if (NugetConfigFileName.Equals(stream.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                await this.ProcessAdditionalDirectory(processRequest, ignoreNugetConfig);
            }
            else
            {
                await this.ProcessFile(processRequest);
            }
        }

        private async Task ProcessAdditionalDirectory(ProcessRequest processRequest, bool ignoreNugetConfig)
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
                        this.Logger.LogInfo($"Found path override in nuget configuration file. Adding {additionalPath} to the package search path.");
                        this.Logger.LogWarning($"Path {additionalPath} is not rooted in the source tree. More components may be detected than expected if this path is shared across code projects.");

                        this.Scanner.Initialize(additionalPath, (name, directoryName) => false, 1);

                        await this.Scanner.GetFilteredComponentStreamObservable(additionalPath, this.SearchPatterns.Where(sp => !NugetConfigFileName.Equals(sp)), singleFileComponentRecorder.GetParentComponentRecorder())
                            .ForEachAsync(async fi => await this.ProcessFile(fi));
                    }
                }
            }
        }

        private async Task ProcessFile(ProcessRequest processRequest)
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
                    nuspecBytes = await NuGetNuspecUtilities.GetNuspecBytesFromNuspecStream(stream.Stream, stream.Stream.Length);
                }
                else if ("paket.lock".Equals(stream.Pattern, StringComparison.OrdinalIgnoreCase))
                {
                    ParsePaketLock(processRequest);
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

                var name = metadataNode["id"].InnerText;
                var version = metadataNode["version"].InnerText;

                var authors = metadataNode["authors"]?.InnerText.Split(",").Select(author => author.Trim()).ToArray();

                if (!NuGetVersion.TryParse(version, out var parsedVer))
                {
                    this.Logger.LogInfo($"Version '{version}' from {stream.Location} could not be parsed as a NuGet version");

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
                this.Logger.LogFailedReadingFile(stream.Location, e);
            }
        }

        private void ParsePaketLock(ProcessRequest processRequest)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var stream = processRequest.ComponentStream;

            using StreamReader reader = new StreamReader(stream.Stream);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var matches = Regex.Matches(line, @"\s*([a-zA-Z0-9-.]*) \([<>=]*[ ]*([0-9a-zA-Z-.]*)\)", RegexOptions.Singleline);
                foreach (Match match in matches)
                {
                    string name = match.Groups[1].Value;
                    string version = match.Groups[2].Value;
                    NuGetComponent component = new NuGetComponent(name, version);
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(component));
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
                    this.Logger.LogWarning($"Excluding discovered path {potentialPath} from location {componentStream.Location} as it could not be determined to be valid.");
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
            FileInfo fileInfo = null;

            try
            {
                fileInfo = new FileInfo(potentialPath);
            }
            catch
            {
                return false;
            }

            if (fileInfo == null)
            {
                return false;
            }

            return fileInfo.Exists || fileInfo.Directory.Exists;
        }
    }
}
