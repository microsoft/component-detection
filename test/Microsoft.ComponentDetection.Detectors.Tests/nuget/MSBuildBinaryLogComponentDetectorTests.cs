namespace Microsoft.ComponentDetection.Detectors.Tests.NuGet;

using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
public class MSBuildBinaryLogComponentDetectorTests : BaseDetectorTest<MSBuildBinaryLogComponentDetector>
{
    private readonly Mock<IFileUtilityService> fileUtilityServiceMock;

    public MSBuildBinaryLogComponentDetectorTests()
    {
        this.fileUtilityServiceMock = new Mock<IFileUtilityService>();
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        this.DetectorTestUtility.AddServiceMock(this.fileUtilityServiceMock);
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_WithSimpleAssetsFile_DetectsComponents()
    {
        var projectAssetsJson = @"{
            ""version"": 3,
            ""targets"": {
                ""net8.0"": {
                    ""Newtonsoft.Json/13.0.1"": {
                        ""type"": ""package"",
                        ""compile"": {
                            ""lib/net8.0/Newtonsoft.Json.dll"": {}
                        },
                        ""runtime"": {
                            ""lib/net8.0/Newtonsoft.Json.dll"": {}
                        }
                    }
                }
            },
            ""libraries"": {
                ""Newtonsoft.Json/13.0.1"": {
                    ""sha512"": ""test"",
                    ""type"": ""package"",
                    ""path"": ""newtonsoft.json/13.0.1"",
                    ""files"": [
                        ""lib/net8.0/Newtonsoft.Json.dll""
                    ]
                }
            },
            ""projectFileDependencyGroups"": {
                ""net8.0"": [
                    ""Newtonsoft.Json >= 13.0.1""
                ]
            },
            ""packageFolders"": {
                ""C:\\Users\\test\\.nuget\\packages\\"": {}
            },
            ""project"": {
                ""version"": ""1.0.0"",
                ""restore"": {
                    ""projectName"": ""TestProject"",
                    ""projectPath"": ""C:\\test\\TestProject.csproj"",
                    ""outputPath"": ""C:\\test\\obj""
                },
                ""frameworks"": {
                    ""net8.0"": {
                        ""targetAlias"": ""net8.0"",
                        ""dependencies"": {
                            ""Newtonsoft.Json"": {
                                ""target"": ""Package"",
                                ""version"": ""[13.0.1, )""
                            }
                        }
                    }
                }
            }
        }";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", projectAssetsJson)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(1);

        var component = detectedComponents.First();
        var nugetComponent = component.Component as NuGetComponent;
        nugetComponent.Should().NotBeNull();
        nugetComponent!.Name.Should().Be("Newtonsoft.Json");
        nugetComponent.Version.Should().Be("13.0.1");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_WithTransitiveDependencies_BuildsDependencyGraph()
    {
        var projectAssetsJson = @"{
            ""version"": 3,
            ""targets"": {
                ""net8.0"": {
                    ""Microsoft.Extensions.Logging/8.0.0"": {
                        ""type"": ""package"",
                        ""dependencies"": {
                            ""Microsoft.Extensions.Logging.Abstractions"": ""8.0.0""
                        },
                        ""compile"": {
                            ""lib/net8.0/Microsoft.Extensions.Logging.dll"": {}
                        },
                        ""runtime"": {
                            ""lib/net8.0/Microsoft.Extensions.Logging.dll"": {}
                        }
                    },
                    ""Microsoft.Extensions.Logging.Abstractions/8.0.0"": {
                        ""type"": ""package"",
                        ""compile"": {
                            ""lib/net8.0/Microsoft.Extensions.Logging.Abstractions.dll"": {}
                        },
                        ""runtime"": {
                            ""lib/net8.0/Microsoft.Extensions.Logging.Abstractions.dll"": {}
                        }
                    }
                }
            },
            ""libraries"": {
                ""Microsoft.Extensions.Logging/8.0.0"": {
                    ""sha512"": ""test"",
                    ""type"": ""package"",
                    ""path"": ""microsoft.extensions.logging/8.0.0"",
                    ""files"": [
                        ""lib/net8.0/Microsoft.Extensions.Logging.dll""
                    ]
                },
                ""Microsoft.Extensions.Logging.Abstractions/8.0.0"": {
                    ""sha512"": ""test"",
                    ""type"": ""package"",
                    ""path"": ""microsoft.extensions.logging.abstractions/8.0.0"",
                    ""files"": [
                        ""lib/net8.0/Microsoft.Extensions.Logging.Abstractions.dll""
                    ]
                }
            },
            ""projectFileDependencyGroups"": {
                ""net8.0"": [
                    ""Microsoft.Extensions.Logging >= 8.0.0""
                ]
            },
            ""packageFolders"": {
                ""C:\\Users\\test\\.nuget\\packages\\"": {}
            },
            ""project"": {
                ""version"": ""1.0.0"",
                ""restore"": {
                    ""projectName"": ""TestProject"",
                    ""projectPath"": ""C:\\test\\TestProject.csproj"",
                    ""outputPath"": ""C:\\test\\obj""
                },
                ""frameworks"": {
                    ""net8.0"": {
                        ""targetAlias"": ""net8.0"",
                        ""dependencies"": {
                            ""Microsoft.Extensions.Logging"": {
                                ""target"": ""Package"",
                                ""version"": ""[8.0.0, )""
                            }
                        }
                    }
                }
            }
        }";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", projectAssetsJson)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var graphsByLocation = componentRecorder.GetDependencyGraphsByLocation();
        graphsByLocation.Should().NotBeEmpty();

        var graph = graphsByLocation.Values.First();
        var loggingComponent = detectedComponents.First(x => ((NuGetComponent)x.Component).Name == "Microsoft.Extensions.Logging");
        var abstractionsComponent = detectedComponents.First(x => ((NuGetComponent)x.Component).Name == "Microsoft.Extensions.Logging.Abstractions");

        graph.IsComponentExplicitlyReferenced(loggingComponent.Component.Id).Should().BeTrue();
        graph.IsComponentExplicitlyReferenced(abstractionsComponent.Component.Id).Should().BeFalse();

        var dependencies = graph.GetDependenciesForComponent(loggingComponent.Component.Id);
        dependencies.Should().Contain(abstractionsComponent.Component.Id);
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_WithNoPackageSpec_HandlesGracefully()
    {
        var projectAssetsJson = @"{
            ""version"": 3,
            ""targets"": {
                ""net8.0"": {}
            },
            ""libraries"": {},
            ""packageFolders"": {}
        }";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", projectAssetsJson)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_WithProjectReference_ExcludesProjectDependencies()
    {
        var projectAssetsJson = @"{
            ""version"": 3,
            ""targets"": {
                ""net8.0"": {
                    ""Newtonsoft.Json/13.0.1"": {
                        ""type"": ""package"",
                        ""compile"": {
                            ""lib/net8.0/Newtonsoft.Json.dll"": {}
                        },
                        ""runtime"": {
                            ""lib/net8.0/Newtonsoft.Json.dll"": {}
                        }
                    },
                    ""MyOtherProject/1.0.0"": {
                        ""type"": ""project""
                    }
                }
            },
            ""libraries"": {
                ""Newtonsoft.Json/13.0.1"": {
                    ""sha512"": ""test"",
                    ""type"": ""package"",
                    ""path"": ""newtonsoft.json/13.0.1"",
                    ""files"": [
                        ""lib/net8.0/Newtonsoft.Json.dll""
                    ]
                },
                ""MyOtherProject/1.0.0"": {
                    ""type"": ""project"",
                    ""path"": ""../MyOtherProject/MyOtherProject.csproj"",
                    ""msbuildProject"": ""../MyOtherProject/MyOtherProject.csproj""
                }
            },
            ""projectFileDependencyGroups"": {
                ""net8.0"": [
                    ""Newtonsoft.Json >= 13.0.1""
                ]
            },
            ""packageFolders"": {
                ""C:\\Users\\test\\.nuget\\packages\\"": {}
            },
            ""project"": {
                ""version"": ""1.0.0"",
                ""restore"": {
                    ""projectName"": ""TestProject"",
                    ""projectPath"": ""C:\\test\\TestProject.csproj"",
                    ""outputPath"": ""C:\\test\\obj""
                },
                ""frameworks"": {
                    ""net8.0"": {
                        ""targetAlias"": ""net8.0"",
                        ""dependencies"": {
                            ""Newtonsoft.Json"": {
                                ""target"": ""Package"",
                                ""version"": ""[13.0.1, )""
                            }
                        }
                    }
                }
            }
        }";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", projectAssetsJson)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should only detect the NuGet package, not the project reference
        detectedComponents.Should().HaveCount(1);
        var component = detectedComponents.First().Component as NuGetComponent;
        component!.Name.Should().Be("Newtonsoft.Json");
    }

    [TestMethod]
    public async Task ScanDirectoryAsync_WithDevelopmentDependency_MarksAsDev()
    {
        // A package with only compile/build assets and no runtime assemblies should be marked as a dev dependency
        var projectAssetsJson = @"{
            ""version"": 3,
            ""targets"": {
                ""net8.0"": {
                    ""StyleCop.Analyzers/1.2.0-beta.556"": {
                        ""type"": ""package"",
                        ""compile"": {
                            ""lib/netstandard2.0/_._"": {}
                        },
                        ""runtime"": {
                            ""lib/netstandard2.0/_._"": {}
                        },
                        ""build"": {
                            ""build/StyleCop.Analyzers.props"": {}
                        }
                    }
                }
            },
            ""libraries"": {
                ""StyleCop.Analyzers/1.2.0-beta.556"": {
                    ""sha512"": ""test"",
                    ""type"": ""package"",
                    ""path"": ""stylecop.analyzers/1.2.0-beta.556"",
                    ""files"": [
                        ""analyzers/dotnet/cs/StyleCop.Analyzers.dll"",
                        ""lib/netstandard2.0/_._""
                    ]
                }
            },
            ""projectFileDependencyGroups"": {
                ""net8.0"": [
                    ""StyleCop.Analyzers >= 1.2.0-beta.556""
                ]
            },
            ""packageFolders"": {
                ""C:\\Users\\test\\.nuget\\packages\\"": {}
            },
            ""project"": {
                ""version"": ""1.0.0"",
                ""restore"": {
                    ""projectName"": ""TestProject"",
                    ""projectPath"": ""C:\\test\\TestProject.csproj"",
                    ""outputPath"": ""C:\\test\\obj""
                },
                ""frameworks"": {
                    ""net8.0"": {
                        ""targetAlias"": ""net8.0"",
                        ""dependencies"": {
                            ""StyleCop.Analyzers"": {
                                ""target"": ""Package"",
                                ""version"": ""[1.2.0-beta.556, )""
                            }
                        }
                    }
                }
            }
        }";

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("project.assets.json", projectAssetsJson)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(1);

        var component = detectedComponents.First();

        // Analyzers are detected as development dependencies because they have analyzers in files
        // but their runtime assets are placeholders
        componentRecorder.GetEffectiveDevDependencyValue(component.Component.Id).Should().NotBeNull();
    }
}
