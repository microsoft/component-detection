using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Signing;
using NuGet.Versioning;

namespace Microsoft.ComponentDetection.Detectors.Npm
{
    [Export(typeof(IComponentDetector))]
    public class NpmComponentDetector : FileComponentDetector
    {
        public override string Id { get; } = "Npm";

        public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Npm) };

        public override IList<string> SearchPatterns { get; } = new List<string> { "package.json" };

        public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Npm };

        public override int Version { get; } = 2;

        /// <summary>Common delegate for Package.json JToken processing.</summary>
        /// <param name="token">A JToken, usually corresponding to a package.json file.</param>
        /// <returns>Used in scenarios where one file path creates multiple JTokens, a false value indicates processing additional JTokens should be halted, proceed otherwise.</returns>
        protected delegate bool JTokenProcessingDelegate(JToken token);

        protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
        {
            var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
            var file = processRequest.ComponentStream;

            var filePath = file.Location;

            string contents;
            using (var reader = new StreamReader(file.Stream))
            {
                contents = await reader.ReadToEndAsync();
            }

            await SafeProcessAllPackageJTokens(filePath, contents, (token) =>
            {
                if (token["name"] == null || token["version"] == null)
                {
                    Logger.LogInfo($"{filePath} does not contain a name and/or version. These are required fields for a valid package.json file." +
                                   $"It and its dependencies will not be registered.");
                    return false;
                }

                return ProcessIndividualPackageJTokens(filePath, singleFileComponentRecorder, token);
            });
        }

        private async Task SafeProcessAllPackageJTokens(string sourceFilePath, string contents, JTokenProcessingDelegate jtokenProcessor)
        {
            try
            {
                await ProcessAllPackageJTokensAsync(contents, jtokenProcessor);
            }
            catch (Exception e)
            {
                // If something went wrong, just ignore the component
                Logger.LogBuildWarning($"Could not parse Jtokens from file {sourceFilePath}.");
                Logger.LogFailedReadingFile(sourceFilePath, e);
                return;
            }
        }

        protected virtual Task ProcessAllPackageJTokensAsync(string contents, JTokenProcessingDelegate jtokenProcessor)
        {
            var o = JToken.Parse(contents);
            jtokenProcessor(o);
            return Task.CompletedTask;
        }

        protected virtual bool ProcessIndividualPackageJTokens(string filePath, ISingleFileComponentRecorder singleFileComponentRecorder, JToken packageJToken)
        {
            var name = packageJToken["name"].ToString();
            var version = packageJToken["version"].ToString();
            var authorToken = packageJToken["author"];
            
            if (!SemanticVersion.TryParse(version, out _))
            {
                Logger.LogWarning($"Unable to parse version \"{version}\" for package \"{name}\" found at path \"{filePath}\". This may indicate an invalid npm package component and it will not be registered.");
                return false;
            }

            var npmComponent = new NpmComponent(name, version);
            npmComponent.Author = GetAuthor(authorToken, name, filePath);

            var detectedComponent = new DetectedComponent(npmComponent);
            
            singleFileComponentRecorder.RegisterUsage(detectedComponent);
            return true;
        }

        private NpmAuthor GetAuthor(JToken authorToken, string packageName, string filePath)
        {
            var authorString = authorToken?.ToString();
            if (string.IsNullOrEmpty(authorString))
            {
                return null;
            }

            string authorName = null;
            string authorEmail = null;

            //for parsing author in Json Format
            // for e.g. 
            // "author": {
            //     "name": "John Doe",
            //     "email": "johndoe@outlook.com",
            //     "name": "https://jd.com",
            // }
            if (authorString.StartsWith("{"))
            {
                authorName = authorToken["name"]?.ToString();
                authorEmail = authorToken["email"]?.ToString();
            }

            // for parsing single line author.
            // for e.g. "author": "John Doe <johnDoe@outlook.com> (https://jd.com)"
            else
            {
                bool isEmailPresent = authorString.Contains("<");
                bool isUrlPresent = authorString.Contains("(");
                if (!isEmailPresent)
                {
                    if (isUrlPresent)
                    {
                        authorName = authorString.Substring(0, authorString.IndexOf("(")).Trim();
                    }
                    else
                    {
                        authorName = authorString.Trim();
                    }
                }
                else
                {
                    authorName = authorString.Substring(0, authorString.IndexOf("<")).Trim();
                    authorEmail = authorString.Substring(authorString.IndexOf("<") + 1, authorString.IndexOf(">") - authorString.IndexOf("<") - 1);
                }
            }

            if (string.IsNullOrEmpty(authorName))
            {
                Logger.LogWarning(string.Format(
                    "Unable to parse author: {0} for package: {1} found at path {2}. " +
                    "This may indicate an invalid npm package author, and it will not be registered.", authorString, packageName, filePath));
                return null;
            }

            return new NpmAuthor(authorName, authorEmail);
        }
    }
}