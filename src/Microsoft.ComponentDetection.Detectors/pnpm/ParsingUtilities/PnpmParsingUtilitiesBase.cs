#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pnpm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

internal abstract class PnpmParsingUtilitiesBase<T>
where T : PnpmYaml
{
    public virtual List<T> DeserializePnpmYamlFileDocuments(string fileContent)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        var reader = new StringReader(fileContent);
        var parser = new Parser(reader);
        parser.Consume<StreamStart>();

        var documents = new List<T>();
        while (parser.TryConsume<DocumentStart>(out _))
        {
            var doc = deserializer.Deserialize<T>(parser);
            if (doc != null)
            {
                documents.Add(doc);
            }

            parser.TryConsume<DocumentEnd>(out _);
        }

        return documents;
    }

    public T DeserializePnpmYamlFile(string fileContent)
    {
        return this.DeserializePnpmYamlFileDocuments(fileContent).FirstOrDefault();
    }

    public virtual bool IsPnpmPackageDevDependency(Package pnpmPackage)
    {
        ArgumentNullException.ThrowIfNull(pnpmPackage);

        return string.Equals(bool.TrueString, pnpmPackage.Dev, StringComparison.InvariantCultureIgnoreCase);
    }

    public bool IsLocalDependency(KeyValuePair<string, string> dependency)
    {
        // Local dependencies are dependencies that live in the file system
        // this requires an extra parsing that is not supported yet
        return dependency.Key.StartsWith(PnpmConstants.PnpmFileDependencyPath) || dependency.Value.StartsWith(PnpmConstants.PnpmFileDependencyPath) || dependency.Value.StartsWith(PnpmConstants.PnpmLinkDependencyPath);
    }

    /// <summary>
    /// Parse a pnpm path of the form "/package-name/version and create an npm component".
    /// </summary>
    /// <param name="pnpmPackagePath">a pnpm path of the form "/package-name/version".</param>
    /// <returns>Data parsed from path.</returns>
    public abstract DetectedComponent CreateDetectedComponentFromPnpmPath(string pnpmPackagePath);

    /// <summary>
    /// Parse a pnpm path of the form "/package-name/version into a packageName and Version.
    /// </summary>
    /// <param name="pnpmPackagePath">a pnpm path of the form "/package-name/version".</param>
    /// <returns>Data parsed from path.</returns>
    public abstract (string FullPackageName, string PackageVersion) ExtractNameAndVersionFromPnpmPackagePath(string pnpmPackagePath);

    public virtual string ReconstructPnpmDependencyPath(string dependencyName, string dependencyVersion)
    {
        if (dependencyVersion.StartsWith('/'))
        {
            return dependencyVersion;
        }
        else
        {
            return $"/{dependencyName}@{dependencyVersion}";
        }
    }
}
