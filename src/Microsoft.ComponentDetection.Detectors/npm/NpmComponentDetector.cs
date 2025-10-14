#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Npm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using global::NuGet.Versioning;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

public class NpmComponentDetector : FileComponentDetector
{
    private static readonly Regex SingleAuthor = new Regex(@"^(?<name>([^<(]+?)?)[ \t]*(?:<(?<email>([^>(]+?))>)?[ \t]*(?:\(([^)]+?)\)|$)", RegexOptions.Compiled);

    public NpmComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<NpmComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    /// <summary>Common delegate for Package.json JToken processing.</summary>
    /// <param name="token">A JToken, usually corresponding to a package.json file.</param>
    /// <returns>Used in scenarios where one file path creates multiple JTokens, a false value indicates processing additional JTokens should be halted, proceed otherwise.</returns>
    protected delegate bool JTokenProcessingDelegate(JToken token);

    public override string Id { get; } = "Npm";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.Npm)];

    public override IList<string> SearchPatterns { get; } = ["package.json"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.Npm];

    public override int Version { get; } = 3;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        var filePath = file.Location;

        string contents;
        using (var reader = new StreamReader(file.Stream))
        {
            contents = await reader.ReadToEndAsync(cancellationToken);
        }

        await this.SafeProcessAllPackageJTokensAsync(filePath, contents, (token) =>
        {
            if (token["name"] == null || token["version"] == null)
            {
                this.Logger.LogInformation("{BadPackageJson} does not contain a name and/or version. These are required fields for a valid package.json file. It and its dependencies will not be registered.", filePath);
                return false;
            }

            return this.ProcessIndividualPackageJTokens(filePath, singleFileComponentRecorder, token);
        });
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
        var enginesToken = packageJToken["engines"];

        if (!SemanticVersion.TryParse(version, out _))
        {
            this.Logger.LogWarning("Unable to parse version {NpmPackageVersion} for package {NpmPackageName} found at path {NpmPackageLocation}. This may indicate an invalid npm package component and it will not be registered.", version, name, filePath);
            singleFileComponentRecorder.RegisterPackageParseFailure($"{name} - {version}");
            return false;
        }

        var containsVsCodeEngine = false;
        if (enginesToken != null)
        {
            if (enginesToken.Type == JTokenType.Array)
            {
                var engineStrings = enginesToken
                    .Children()
                    .Where(t => t.Type == JTokenType.String)
                    .Select(t => t.ToString())
                    .ToArray();
                if (engineStrings.Any(e => e.Contains("vscode")))
                {
                    containsVsCodeEngine = true;
                }
            }
            else if (enginesToken.Type == JTokenType.Object)
            {
                if (enginesToken["vscode"] != null)
                {
                    containsVsCodeEngine = true;
                }
            }
        }

        if (containsVsCodeEngine)
        {
            this.Logger.LogInformation("{NpmPackageName} found at path {NpmPackageLocation} represents a built-in VS Code extension. This package will not be registered.", name, filePath);
            return false;
        }

        var npmComponent = new NpmComponent(name, version, author: this.GetAuthor(authorToken, name, filePath));

        singleFileComponentRecorder.RegisterUsage(new DetectedComponent(npmComponent));
        return true;
    }

    private async Task SafeProcessAllPackageJTokensAsync(string sourceFilePath, string contents, JTokenProcessingDelegate jtokenProcessor)
    {
        try
        {
            await this.ProcessAllPackageJTokensAsync(contents, jtokenProcessor);
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the component
            this.Logger.LogInformation(e, "Could not parse Jtokens from file {PackageJsonFilePaths}.", sourceFilePath);
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
        var authorMatch = SingleAuthor.Match(authorString);
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
            this.Logger.LogWarning("Unable to parse author:[{NpmAuthorString}] for package:[{NpmPackageName}] found at path:[{NpmPackageLocation}]. This may indicate an invalid npm package author, and author will not be registered.", authorString, packageName, filePath);
            return null;
        }

        if (string.IsNullOrEmpty(authorName))
        {
            this.Logger.LogWarning("Unable to parse author:[{NpmAuthorString}] for package:[{NpmPackageName}] found at path:[{NpmPackageLocation}]. This may indicate an invalid npm package author, and author will not be registered.", authorString, packageName, filePath);
            return null;
        }

        return new NpmAuthor(authorName, authorEmail);
    }
}
