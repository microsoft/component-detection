namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
using System.Xml;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class MavenFileParserService : IMavenFileParserService
{
    private readonly ILogger<MavenFileParserService> logger;

    public MavenFileParserService(
       ILogger<MavenFileParserService> logger) => this.logger = logger;

    public void ParseDependenciesFile(ProcessRequest processRequest)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var stream = processRequest.ComponentStream;

        try
        {
            var doc = new XmlDocument();
            doc.Load(stream.Location);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ns", "http://maven.apache.org/POM/4.0.0");

            var dependencies = doc.SelectSingleNode("//ns:project/ns:dependencies", nsmgr);
            if (dependencies == null)
            {
                return;
            }

            foreach (XmlNode node in dependencies.ChildNodes)
            {
                this.RegisterComponent(node, nsmgr, singleFileComponentRecorder);
            }
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the component
            this.logger.LogError(e, "Error parsing pom maven component from {PomLocation}", stream.Location);
            singleFileComponentRecorder.RegisterPackageParseFailure(stream.Location);
        }
    }

    private void RegisterComponent(XmlNode node, XmlNamespaceManager nsmgr, ISingleFileComponentRecorder singleFileComponentRecorder)
    {
        var groupIdNode = node.SelectSingleNode("ns:groupId", nsmgr);
        var artifactIdNode = node.SelectSingleNode("ns:artifactId", nsmgr);
        var versionNode = node.SelectSingleNode("ns:version", nsmgr);

        if (groupIdNode == null || artifactIdNode == null || versionNode == null)
        {
            this.logger.LogInformation("{XmlNode} doesn't have groupId, artifactId or version information", node.InnerText);
            return;
        }

        var groupId = groupIdNode.InnerText;
        var artifactId = artifactIdNode.InnerText;
        var version = versionNode.InnerText;
        var dependencyScope = DependencyScope.MavenCompile;

        var component = new MavenComponent(groupId, artifactId, version);

        singleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(component),
                    isDevelopmentDependency: null,
                    dependencyScope: dependencyScope);
    }
}
