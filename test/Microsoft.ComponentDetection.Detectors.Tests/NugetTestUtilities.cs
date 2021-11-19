using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Moq;
using Microsoft.ComponentDetection.TestsUtilities;

using static Microsoft.ComponentDetection.Detectors.Tests.Utilities.TestUtilityExtensions;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    public static class NugetTestUtilities
    {
        public static string GetRandomValidNuSpecComponent()
        {
            string componentName = Guid.NewGuid().ToString("N");
            string componentSpecFileName = $"{componentName}.nuspec";
            string componentSpecPath = Path.Combine(Path.GetTempPath(), componentSpecFileName);
            var template = GetTemplatedNuspec(componentName, NewRandomVersion());

            return template;
        }

        public static IComponentStream GetRandomValidNuSpecComponentStream()
        {
            string componentName = Guid.NewGuid().ToString("N");
            string componentSpecFileName = $"{componentName}.nuspec";
            string componentSpecPath = Path.Combine(Path.GetTempPath(), componentSpecFileName);
            var template = GetTemplatedNuspec(componentName, NewRandomVersion());

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

        public static string GetRandomValidNuPkgComponent()
        {
            string componentName = Guid.NewGuid().ToString("N");
            var template = GetTemplatedNuspec(componentName, NewRandomVersion());
            return template;
        }

        public static async Task<Stream> ZipNupkgComponent(string filename, string content)
        {
            var stream = new MemoryStream();

            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry($"{filename}.nuspec");

                using var entryStream = entry.Open();

                var templateBytes = Encoding.UTF8.GetBytes(content);
                await entryStream.WriteAsync(templateBytes, 0, templateBytes.Length);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static string GetRandomMalformedNuPkgComponent()
        {
            string componentName = Guid.NewGuid().ToString("N");
            var template = GetTemplatedNuspec(componentName, NewRandomVersion());
            template = template.Replace("<id>", "<?malformed>");
            return template;
        }

        private static string GetTemplatedNuspec(string id, string version)
        {
            string nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                                <metadata>
                                    <!-- Required elements-->
                                    <id>{0}</id>
                                    <version>{1}</version>
                                    <description></description>
                                    <authors></authors>

                                    <!-- Optional elements -->
                                    <!-- ... -->
                                </metadata>
                                <!-- Optional 'files' node -->
                            </package>";

            return string.Format(nuspec, id, version);
        }

        private static string GetTemplatedNuGetConfig(string repositoryPath)
        {
            string nugetConfig =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                    <config>
                        <add key=""repositoryPath"" value=""{0}"" />
                    </config>
                </configuration>";
            return string.Format(nugetConfig, repositoryPath);
        }

        private static string GetTemplatedProjectAsset(IDictionary<string, IEnumerable<KeyValuePair<string, string>>> packages)
        {
            string individualPackageJson =
                @"""{packageName}"": {
                      ""type"": ""package"",
                      ""dependencies"": {
                          {dependencyList}
                      }
                  }";

            var packageJsons = new List<string>();
            foreach (var package in packages)
            {
                var packageName = package.Key;
                var dependencyList = string.Join(",", package.Value.Select(d => string.Format(@"""{0}"":""{1}""", d.Key, d.Value)));
                packageJsons.Add(individualPackageJson.Replace("{packageName}", packageName).Replace("{dependencyList}", dependencyList));
            }

            var allPackageJson = string.Join(",", packageJsons);
            string pojectAssetsJson =
                @"{
                      ""version"": 2,
                      ""targets"": {
                            "".NETCoreApp,Version=v2.1"": {
                                {allPackageJson}
                            },
                            "".NETFramework,Version=v4.6.1"": {
                                {allPackageJson}
                            }
                      },
                      ""project"": {
                        ""version"": ""1.0.0"",
                        ""restore"": {
                          ""projectPath"": ""myproject.csproj""
                        },
                        ""frameworks"": {
                          ""netcoreapp2.0"": {
                            ""dependencies"": {
                              ""Microsoft.NETCore.App"": {
                                ""target"": ""Package"",
                                ""version"": ""[2.0.0, )""
                              }
                            }
                          }
                        }
                      }
                }";
            return pojectAssetsJson.Replace("{allPackageJson}", allPackageJson);
        }
    }
}
