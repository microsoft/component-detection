#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

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

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetPackageJsonOneRootOneDevDependencyOneOptionalDependency(
        string rootName, string rootVersion, string devDependencyName, string devDependencyVersion, string optionalDependencyName, string optionalDependencyVersion)
    {
        var packagejson = @"{{
                ""name"": ""{0}"",
                ""version"": ""{1}"",
                ""devDependencies"": {{
                    ""{2}"": ""{3}""
                }},
                ""optionalDependencies"": {{
                    ""{4}"": ""{5}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, rootName, rootVersion, devDependencyName, devDependencyVersion, optionalDependencyName, optionalDependencyVersion);

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

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetPackageJsonNoDependenciesForNameAndVersionWithNodeEngine(string packageName, string packageVersion)
    {
        var packagejson = @"{{
                ""name"": ""{0}"",
                ""version"": ""{1}"",
                ""engines"": {{
                    ""node"": ""^20.0.0""
                }}
            }}";
        var packageJsonTemplate = string.Format(packagejson, packageName, packageVersion);
        return ("package.json", packageJsonTemplate, Path.Combine(Path.GetTempPath(), "package.json"));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetPackageJsonNoDependenciesForNameAndVersionWithVSCodeEngine(string packageName, string packageVersion)
    {
        var packagejson = @"{{
                ""name"": ""{0}"",
                ""version"": ""{1}"",
                ""engines"": {{
                    ""vscode"": ""^1.0.0""
                }}
            }}";
        var packageJsonTemplate = string.Format(packagejson, packageName, packageVersion);
        return ("package.json", packageJsonTemplate, Path.Combine(Path.GetTempPath(), "package.json"));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetPackageJsonNoDependenciesForNameAndVersionWithEngiesAsArray(string packageName, string packageVersion, string engineText)
    {
        var packagejson = @"{{
                ""name"": ""{0}"",
                ""version"": ""{1}"",
                ""engines"": [
                    ""{2}""
                ]
            }}";
        var packageJsonTemplate = string.Format(packagejson, packageName, packageVersion, engineText);
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

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetWellFormedPackageLock2WithOptionalAndDevDependency(
        string lockFileName, string rootName = null, string rootVersion = null, string devDependencyName = null, string devDependencyVersion = null, string optionalDependencyName = null, string optionalDependencyVersion = null)
    {
        var packageLockJson = @"{{
                ""name"": ""{0}"",
                ""version"": ""{1}"",
                ""dependencies"": {{
                    ""{2}"": {{
                        ""version"": ""{3}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""dev"": true,
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""optional"": true,
                    }}
                }}
            }}";
        rootName ??= Guid.NewGuid().ToString("N");
        rootVersion ??= NewRandomVersion();
        devDependencyName ??= Guid.NewGuid().ToString("N");
        devDependencyVersion ??= NewRandomVersion();
        optionalDependencyName ??= Guid.NewGuid().ToString("N");
        optionalDependencyVersion ??= NewRandomVersion();
        var packageLockTemplate = string.Format(packageLockJson, rootName, rootVersion, devDependencyName, devDependencyVersion, optionalDependencyName, optionalDependencyVersion);

        return (lockFileName, packageLockTemplate, Path.Combine(Path.GetTempPath(), lockFileName));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetWellFormedPackageLock3(string lockFileName, string rootName0 = null, string rootVersion0 = null, string rootName2 = null, string rootVersion2 = null, string packageName0 = "test", string packageName1 = null, string packageName3 = null)
    {
        var packageLockJson = @"{{
                ""name"": ""{10}"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""packages"": {{
                    """": {{
                        ""dependencies"": {{
                            ""{0}"": ""{1}"",
                            ""{6}"": ""{7}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""dependencies"": {{
                            ""{2}"": ""{3}""
                        }}
                    }},
                    ""node_modules/{2}"": {{
                        ""version"": ""{3}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""dependencies"": {{
                            ""{4}"": ""{5}""
                        }}
                    }},
                    ""node_modules/{6}"": {{
                        ""version"": ""{7}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""dependencies"": {{
                            ""{8}"": ""{9}""
                        }}
                    }},
                    ""node_modules/{8}"": {{
                        ""version"": ""{9}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""dependencies"": {{
                            ""{4}"": ""{5}""
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

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetWellFormedNestedPackageLock3(string lockFileName, string rootName0 = null, string rootVersion0 = null, string rootName1 = null, string rootVersion1 = null, string sharedName0 = null)
    {
        var packageLockJson = @"{{
              ""name"": ""test"",
              ""version"": ""0.0.0"",
              ""lockfileVersion"": 3,
              ""requires"": true,
              ""packages"": {{
                """": {{
                  ""name"": ""test"",
                  ""version"": ""0.0.0"",
                  ""dependencies"": {{
                    ""{0}"": ""^{3}"",
                    ""{1}"": ""^{4}""
                  }}
                }},
                ""node_modules/{0}"": {{
                  ""version"": ""{3}"",
                  ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry"",
                  ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ=="",
                  ""dependencies"": {{
                    ""{2}"": ""2.1.2""
                  }}
                }},
                ""node_modules/{0}/node_modules/{2}"": {{
                  ""version"": ""2.1.2"",
                  ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry"",
                  ""integrity"": ""sha512-W86pkk7P9PAfARThHaD4fIjJ8QJUGMB2OhlCFsrueciPqlYZvDg/w62BmRm7PghVQcxGLbYoPN4+iykzP+0jRQ==""
                }},
                ""node_modules/{1}"": {{
                  ""version"": ""{4}"",
                  ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry"",
                  ""integrity"": ""sha512-VPGZmLZpgqYaa5P4UCrpxC2V9YuneXmNwxVKXCw10iG/UdQSTqyeyNRtwLaCVXPX+wzqzzUa+TujhG787m4Ung=="",
                  ""dependencies"": {{
                    ""{2}"": ""^2.1.1""
                  }}
                }},
                ""node_modules/{2}"": {{
                  ""version"": ""2.1.3"",
                  ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry"",
                  ""integrity"": ""sha512-pitlDcWjRkRQHkOYvmyzK73zfAF2Qq8115BXvVw6KarvBXGSaCbNnQTK7YDORdol3+efRoMzuqXz+UDGcQbhDg==""
                }}
              }}
            }}";

        var componentName0 = rootName0 ?? Guid.NewGuid().ToString("N");
        var version0 = rootVersion0 ?? NewRandomVersion();
        var componentName1 = rootName1 ?? Guid.NewGuid().ToString("N");
        var version1 = rootVersion1 ?? NewRandomVersion();
        var componentName2 = sharedName0 ?? Guid.NewGuid().ToString("N");

        var packageLockTemplate = string.Format(packageLockJson, componentName0, componentName1, componentName2, version0, version1);

        return (lockFileName, packageLockTemplate, Path.Combine(Path.GetTempPath(), lockFileName));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetWellFormedNestedPackageLock3WithDevDependencies(string lockFileName, string depName0 = null, string depVersion0 = null, string depName1 = null, string depVersion1 = null)
    {
        var packageLockJson = @"{{
              ""name"": ""test"",
              ""version"": ""0.0.0"",
              ""lockfileVersion"": 3,
              ""requires"": true,
              ""packages"": {{
                """": {{
                  ""name"": ""test"",
                  ""version"": ""0.0.0"",
                  ""devDependencies"": {{
                    ""{0}"": ""^{2}"",
                    ""{1}"": ""^{3}""
                  }}
                }},
                ""node_modules/{0}"": {{
                  ""version"": ""{2}"",
                  ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry"",
                  ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ=="",
                  ""dev"": true
                }},
                ""node_modules/{1}"": {{
                  ""version"": ""{3}"",
                  ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry"",
                  ""integrity"": ""sha512-W86pkk7P9PAfARThHaD4fIjJ8QJUGMB2OhlCFsrueciPqlYZvDg/w62BmRm7PghVQcxGLbYoPN4+iykzP+0jRQ=="",
                  ""dev"": true
                }}
              }}
            }}";

        var componentName0 = depName0 ?? Guid.NewGuid().ToString("N");
        var version0 = depVersion0 ?? NewRandomVersion();
        var componentName1 = depName1 ?? Guid.NewGuid().ToString("N");
        var version1 = depVersion1 ?? NewRandomVersion();

        var packageLockTemplate = string.Format(packageLockJson, componentName0, componentName1, version0, version1);

        return (lockFileName, packageLockTemplate, Path.Combine(Path.GetTempPath(), lockFileName));
    }

    public static (string PackageJsonName, string PackageJsonContents, string PackageJsonPath) GetWellFormedNestedPackageLock3WithOptionalDependencies(string lockFileName, string depName0 = null, string depVersion0 = null, string depName1 = null, string depVersion1 = null)
    {
        var packageLockJson = @"{{
              ""name"": ""test"",
              ""version"": ""0.0.0"",
              ""lockfileVersion"": 3,
              ""requires"": true,
              ""packages"": {{
                """": {{
                  ""name"": ""test"",
                  ""version"": ""0.0.0"",
                  ""optionalDependencies"": {{
                    ""{0}"": ""^{2}"",
                    ""{1}"": ""^{3}""
                  }}
                }},
                ""node_modules/{0}"": {{
                  ""version"": ""{2}"",
                  ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry"",
                  ""integrity"": ""sha512-nAEMjKcB1LDrMyYnjNsDkxoewI2aexrwlT3UJeL+nlbd64FEQNmKgPGAYIieaLVgtpRiHE9OL6/rmHLlstQwnQ=="",
                  ""optional"": true,
                }},
                ""node_modules/{1}"": {{
                  ""version"": ""{3}"",
                  ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry"",
                  ""integrity"": ""sha512-W86pkk7P9PAfARThHaD4fIjJ8QJUGMB2OhlCFsrueciPqlYZvDg/w62BmRm7PghVQcxGLbYoPN4+iykzP+0jRQ=="",
                  ""optional"": true,
                }}
              }}
            }}";

        var componentName0 = depName0 ?? Guid.NewGuid().ToString("N");
        var version0 = depVersion0 ?? NewRandomVersion();
        var componentName1 = depName1 ?? Guid.NewGuid().ToString("N");
        var version1 = depVersion1 ?? NewRandomVersion();

        var packageLockTemplate = string.Format(packageLockJson, componentName0, componentName1, version0, version1);

        return (lockFileName, packageLockTemplate, Path.Combine(Path.GetTempPath(), lockFileName));
    }
}
