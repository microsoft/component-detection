namespace Microsoft.ComponentDetection.Detectors.Npm;

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json.Linq;

[Export(typeof(IComponentDetector))]
public class NpmComponentDetector : FileComponentDetector
{
    /// <summary>Common delegate for Package.json JToken processing.</summary>
    /// <param name="token">A JToken, usually corresponding to a package.json file.</param>
    /// <returns>Used in scenarios where one file path creates multiple JTokens, a false value indicates processing additional JTokens should be halted, proceed otherwise.</returns>
    protected delegate bool JTokenProcessor(JToken token);

    public override string Id { get; } = "Npm";

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Npm) };

    public override IList<string> SearchPatterns { get; } = new List<string> { "package.json" };

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = new[] { ComponentType.Npm };

    public override int Version { get; } = 2;

    protected override async Task OnFileFound(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        if (processRequest is null)
        {
            throw new ArgumentNullException(nameof(processRequest));
        }

        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        var filePath = file.Location;

        string contents;
        using (var reader = new StreamReader(file.Stream))
        {
            contents = await reader.ReadToEndAsync();
        }

        await this.SafeProcessAllPackageJTokens(filePath, contents, (token) =>
        {
            if (token["name"] == null || token["version"] == null)
            {
                this.Logger.LogInfo($"{filePath} does not contain a name and/or version. These are required fields for a valid package.json file." +
                                    $"It and its dependencies will not be registered.");
                return false;
            }

            return this.ProcessIndividualPackageJTokens(filePath, singleFileComponentRecorder, token);
        });
    }

    protected virtual Task ProcessAllPackageJTokensAsync(string contents, JTokenProcessor jtokenProcessor)
    {
        if (jtokenProcessor is null)
        {
            throw new ArgumentNullException(nameof(jtokenProcessor));
        }

        var o = JToken.Parse(contents);
        jtokenProcessor(o);
        return Task.CompletedTask;
    }

    protected virtual bool ProcessIndividualPackageJTokens(string filePath, ISingleFileComponentRecorder singleFileComponentRecorder, JToken packageJToken)
    {
        if (singleFileComponentRecorder is null)
        {
            throw new ArgumentNullException(nameof(singleFileComponentRecorder));
        }

        if (packageJToken is null)
        {
            throw new ArgumentNullException(nameof(packageJToken));
        }

        var name = packageJToken["name"].ToString();
        var version = packageJToken["version"].ToString();
        var authorToken = packageJToken["author"];

        if (!SemanticVersion.TryParse(version, out _))
        {
            this.Logger.LogWarning($"Unable to parse version \"{version}\" for package \"{name}\" found at path \"{filePath}\". This may indicate an invalid npm package component and it will not be registered.");
            return false;
        }

        var npmComponent = new NpmComponent(name, version, author: this.GetAuthor(authorToken, name, filePath));

        singleFileComponentRecorder.RegisterUsage(new DetectedComponent(npmComponent));
        return true;
    }

    private async Task SafeProcessAllPackageJTokens(string sourceFilePath, string contents, JTokenProcessor jtokenProcessor)
    {
        try
        {
            await this.ProcessAllPackageJTokensAsync(contents, jtokenProcessor);
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the component
            this.Logger.LogInfo($"Could not parse Jtokens from file {sourceFilePath}.");
            this.Logger.LogFailedReadingFile(sourceFilePath, e);
            return;
        }
    }

    private NpmAuthor GetAuthor(JToken authorToken, string packageName, string filePath)
    {
        var authorString = authorToken?.ToString();
        if (string.IsNullOrEmpty(authorString))
        {
            return null;
        }

        string authorName;
        string authorEmail;
        var authorSingleStringPattern = @"^(?<name>([^<(]+?)?)[ \t]*(?:<(?<email>([^>(]+?))>)?[ \t]*(?:\(([^)]+?)\)|$)";
        var authorMatch = new Regex(authorSingleStringPattern).Match(authorString);

        /*
         * for parsing author in Json Format
         * for e.g.
         * "author": {
         *     "name": "John Doe",
         *     "email": "johndoe@outlook.com",
         *     "name": "https://jd.com",
        */
        if (authorToken.HasValues)
        {
            authorName = authorToken["name"]?.ToString();
            authorEmail = authorToken["email"]?.ToString();

            /*
             *  for parsing author in single string format.
             *  for e.g.
             *  "author": "John Doe <johdoe@outlook.com> https://jd.com"
             */
        }
        else if (authorMatch.Success)
        {
            authorName = authorMatch.Groups["name"].ToString().Trim();
            authorEmail = authorMatch.Groups["email"].ToString().Trim();
        }
        else
        {
            this.Logger.LogWarning($"Unable to parse author:[{authorString}] for package:[{packageName}] found at path:[{filePath}]. This may indicate an invalid npm package author, and author will not be registered.");
            return null;
        }

        if (string.IsNullOrEmpty(authorName))
        {
            this.Logger.LogWarning($"Unable to parse author:[{authorString}] for package:[{packageName}] found at path:[{filePath}]. This may indicate an invalid npm package author, and author will not be registered.");
            return null;
        }

        return new NpmAuthor(authorName, authorEmail);
    }
}
