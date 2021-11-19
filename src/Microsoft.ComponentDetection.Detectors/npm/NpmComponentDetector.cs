using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json.Linq;
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

            if (!SemanticVersion.TryParse(version, out _))
            {
                Logger.LogWarning($"Unable to parse version \"{version}\" for package \"{name}\" found at path \"{filePath}\". This may indicate an invalid npm package component and it will not be registered.");
                return false;
            }

            var detectedComponent = new DetectedComponent(new NpmComponent(name, version));
            singleFileComponentRecorder.RegisterUsage(detectedComponent);
            return true;
        }
    }
}