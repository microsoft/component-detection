namespace Microsoft.ComponentDetection.Detectors.Maven;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class MvnPomCliComponentDetector : FileComponentDetector
{
    public MvnPomCliComponentDetector(
     IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
     IObservableDirectoryWalkerFactory walkerFactory,
     ILogger<MvnCliComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "MvnPomCli";

    public override IList<string> SearchPatterns => new List<string>() { "*.pom" };

    public override IEnumerable<string> Categories => new[] { Enum.GetName(typeof(DetectorClass), DetectorClass.Maven) };

    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Maven };

    public override int Version => 2;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        await this.ProcessFileAsync(processRequest);
    }

    private async Task ProcessFileAsync(ProcessRequest processRequest)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var stream = processRequest.ComponentStream;

        try
        {
            byte[] pomBytes = null;

            if ("*.pom".Equals(stream.Pattern, StringComparison.OrdinalIgnoreCase))
            {
                using (var contentStream = File.Open(stream.Location, FileMode.Open))
                {
                    pomBytes = new byte[contentStream.Length];
                    await contentStream.ReadAsync(pomBytes.AsMemory(0, (int)contentStream.Length));

                    using var pomStream = new MemoryStream(pomBytes, false);
                    var doc = new XmlDocument();
                    doc.Load(pomStream);

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
            }
            else
            {
                return;
            }
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the component
            this.Logger.LogError(e, "Error parsing pom maven component from {PomLocation}", stream.Location);
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
