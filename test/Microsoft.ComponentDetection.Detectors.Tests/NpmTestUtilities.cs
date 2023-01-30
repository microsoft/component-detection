﻿namespace Microsoft.ComponentDetection.Detectors.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.TestsUtilities;
using Moq;
using static Microsoft.ComponentDetection.Detectors.Tests.Utilities.TestUtilityExtensions;

public static class NpmTestUtilities
{
    public static string GetPackageJsonNoDependencies()
    {
        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0""
            }}";

        return packagejson;
    }

    public static IComponentStream GetPackageJsonOneRootComponentStream(string componentName0, string version0)
    {
        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0);

        return GetMockedPackageJsonStream(packageJsonTemplate);
    }

    public static IComponentStream GetMockedPackageJsonStream(string content)
    {
        var packageJsonMock = new Mock<IComponentStream>();
        packageJsonMock.SetupGet(x => x.Stream).Returns(content.ToStream());
        packageJsonMock.SetupGet(x => x.Pattern).Returns("package.json");
        packageJsonMock.SetupGet(x => x.Location).Returns(Path.Combine(Path.GetTempPath(), "package.json"));

        return packageJsonMock.Object;
    }

    public static Mock<IObservableDirectoryWalkerFactory> GetMockDirectoryWalker(IEnumerable<IComponentStream> packageLockStreams, IEnumerable<IComponentStream> packageJsonStreams, string directoryName, IEnumerable<IComponentStream> lernaJsonStreams = null, IEnumerable<string> patterns = null, IEnumerable<string> lernaPatterns = null, IComponentRecorder componentRecorder = null)
    {
        var mock = new Mock<IObservableDirectoryWalkerFactory>();
        var components = new List<IComponentStream>();
        componentRecorder ??= new ComponentRecorder();

        if (lernaJsonStreams != null && lernaPatterns != null)
        {
            components.AddRange(lernaJsonStreams);
        }

        components.AddRange(packageLockStreams);
        components.AddRange(packageJsonStreams);

        mock.Setup(x => x.GetFilteredComponentStreamObservable(It.IsAny<DirectoryInfo>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IComponentRecorder>())).Returns(() => components
            .Select(cs => new ProcessRequest
            {
                ComponentStream = cs,
                SingleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(cs.Location),
            }).ToObservable());

        return mock;
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetPackageJsonOneRoot(string componentName0, string version0)
    {
        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0);

        return ("package.json", packageJsonTemplate, Path.Combine(Path.GetTempPath(), "package.json"));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetPackageJsonNoDependenciesForNameAndVersion(string packageName, string packageVersion)
    {
        var packagejson = @"{{
                ""name"": ""{0}"",
                ""version"": ""{1}""
            }}";
        var packageJsonTemplate = string.Format(packagejson, packageName, packageVersion);
        return ("package.json", packageJsonTemplate, Path.Combine(Path.GetTempPath(), "package.json"));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetPackageJsonNoDependenciesForAuthorAndEmailInJsonFormat(
        string authorName, string authorEmail = null)
    {
        string packagejson;
        if (authorEmail != null)
        {
            packagejson = @"{{
                    ""name"": ""test"",
                    ""version"": ""0.0.0"",
                    ""author"": {{
                        ""name"": ""{0}"",
                        ""email"": ""{1}""
                    }}
                }}";
        }
        else
        {
            packagejson = @"{{
                    ""name"": ""test"",
                    ""version"": ""0.0.0"",
                    ""author"": {{
                        ""name"": ""{0}"",
                    }}
                }}";
        }

        var packageJsonTemplate = string.Format(packagejson, authorName, authorEmail);
        return ("package.json", packageJsonTemplate, Path.Combine(Path.GetTempPath(), "package.json"));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetPackageJsonNoDependenciesForAuthorAndEmailAsSingleString(
        string authorName, string authorEmail = null, string authorUrl = null)
    {
        var packagejson = @"{{{{
                    ""name"": ""test"",
                    ""version"": ""0.0.0"",
                    ""author"": {0}
                }}}}";
        string author;

        if (authorEmail != null && authorUrl != null)
        {
            author = @"""{0} <{1}> ({2})""";
        }
        else if (authorEmail == null && authorUrl != null)
        {
            author = @"""{0} ({2})""";
        }
        else if (authorEmail != null && authorUrl == null)
        {
            author = @"""{0} <{1}>""";
        }
        else
        {
            author = @"""{0}""";
        }

        var packageJsonTemplate = string.Format(string.Format(packagejson, author), authorName, authorEmail, authorUrl);
        return ("package.json", packageJsonTemplate, Path.Combine(Path.GetTempPath(), "package.json"));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetPackageJsonNoDependenciesMalformedAuthorAsSingleString(
        string authorName, string authorEmail = null, string authorUrl = null)
    {
        var packagejson = @"{{{{
                    ""name"": ""test"",
                    ""version"": ""0.0.0"",
                    ""author"": {0}
                }}}}";
        string author;

        if (authorEmail != null && authorUrl != null)
        {
            author = @"""{0} <{1} ({2})""";
        }
        else if (authorEmail == null && authorUrl != null)
        {
            author = @"""{0} ({2}""";
        }
        else if (authorEmail != null && authorUrl == null)
        {
            author = @"""{0} <{1}""";
        }
        else
        {
            author = @"""{0}""";
        }

        var packageJsonTemplate = string.Format(string.Format(packagejson, author), authorName, authorEmail, authorUrl);
        return ("package.json", packageJsonTemplate, Path.Combine(Path.GetTempPath(), "package.json"));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetWellFormedPackageLock2(string lockFileName, string rootName0 = null, string rootVersion0 = null, string rootName2 = null, string rootVersion2 = null, string packageName0 = "test", string packageName1 = null, string packageName3 = null)
    {
        var packageLockJson = @"{{
                ""name"": ""{10}"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""dependencies"": {{
                                ""{2}"": {{
                                    ""version"": ""{3}"",
                                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                                    ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg=""
                                }}
                        }},
                        ""requires"": {{
                                ""{4}"": ""{5}""
                        }}
                    }},
                    ""{6}"": {{
                        ""version"": ""{7}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""dependencies"": {{
                                ""{8}"": {{
                                    ""version"": ""{9}"",
                                    ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                                    ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg=""
                                }}
                        }}
                    }}
                }}
            }}";
        var componentName0 = rootName0 ?? Guid.NewGuid().ToString("N");
        var version0 = rootVersion0 ?? NewRandomVersion();
        var componentName1 = packageName1 ?? Guid.NewGuid().ToString("N");
        var version1 = NewRandomVersion();
        var componentName2 = rootName2 ?? Guid.NewGuid().ToString("N");
        var version2 = rootVersion2 ?? NewRandomVersion();
        var componentName3 = packageName3 ?? Guid.NewGuid().ToString("N");
        var version3 = NewRandomVersion();
        var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, componentName1, version1, componentName2, version2, componentName2, version2, componentName3, version3, packageName0);

        return (lockFileName, packageLockTemplate, Path.Combine(Path.GetTempPath(), lockFileName));
    }
}
