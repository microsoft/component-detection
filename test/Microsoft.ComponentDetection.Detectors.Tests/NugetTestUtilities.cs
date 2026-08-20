#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.TestsUtilities;
using Moq;
using static Microsoft.ComponentDetection.Detectors.Tests.Utilities.TestUtilityExtensions;

public static class NugetTestUtilities
{
    public static string GetRandomValidNuSpecComponent()
    {
        var componentName = GetRandomString();
        var componentSpecFileName = $"{componentName}.nuspec";
        var componentSpecPath = Path.Combine(Path.GetTempPath(), componentSpecFileName);
        var template = GetTemplatedNuspec(componentName, NewRandomVersion(), [GetRandomString(), GetRandomString()]);

        return template;
    }

    public static IComponentStream GetRandomValidNuSpecComponentStream()
    {
        var componentName = GetRandomString();
        var componentSpecFileName = $"{componentName}.nuspec";
        var componentSpecPath = Path.Combine(Path.GetTempPath(), componentSpecFileName);
        var template = GetTemplatedNuspec(componentName, NewRandomVersion(), [GetRandomString(), GetRandomString()]);

        var mock = new Mock<IComponentStream>();
        mock.SetupGet(x => x.Stream).Returns(template.ToStream());
        mock.SetupGet(x => x.Pattern).Returns("*.nuspec");
        mock.SetupGet(x => x.Location).Returns(componentSpecPath);

        return mock.Object;
    }

    public static IComponentStream GetValidNuGetConfig(string repositoryPath)
    {
        var template = GetTemplatedNuGetConfig(repositoryPath);

        var mock = new Mock<IComponentStream>();
        mock.SetupGet(x => x.Stream).Returns(template.ToStream());
        mock.Setup(x => x.Location).Returns(Path.Combine(repositoryPath, "nuget.config"));
        mock.Setup(x => x.Pattern).Returns("nuget.config");

        return mock.Object;
    }

    public static string GetRandomValidNuspec()
    {
        var componentName = GetRandomString();
        var template = GetTemplatedNuspec(componentName, NewRandomVersion(), [GetRandomString(), GetRandomString()]);
        return template;
    }

    public static string GetValidNuspec(string componentName, string version, string[] authors)
    {
        return GetTemplatedNuspec(componentName, version, authors);
    }

    public static async Task<Stream> ZipNupkgComponentAsync(string filename, string content)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry($"{filename}.nuspec");

            using var entryStream = entry.Open();

            var templateBytes = Encoding.UTF8.GetBytes(content);
            await entryStream.WriteAsync(templateBytes);
        }

        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    public static string GetRandomMalformedNuPkgComponent()
    {
        var componentName = GetRandomString();
        var template = GetTemplatedNuspec(componentName, NewRandomVersion(), [GetRandomString(), GetRandomString()]);
        template = template.Replace("<id>", "<?malformed>");
        return template;
    }

    private static string GetTemplatedNuspec(string id, string version, string[] authors)
    {
        var nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                                <metadata>
                                    <!-- Required elements-->
                                    <id>{0}</id>
                                    <version>{1}</version>
                                    <description></description>
                                    <authors>{2}</authors>

                                    <!-- Optional elements -->
                                    <!-- ... -->
                                </metadata>
                                <!-- Optional 'files' node -->
                            </package>";

        return string.Format(nuspec, id, version, string.Join(",", authors));
    }

    private static string GetTemplatedNuGetConfig(string repositoryPath)
    {
        var nugetConfig =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    <config>
                        <add key=""repositoryPath"" value=""{0}"" />
                    </config>
                </configuration>";
        return string.Format(nugetConfig, repositoryPath);
    }
}
