#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class MvnCliDetectorTests : BaseDetectorTest<MvnCliComponentDetector>
{
    /// <summary>
    /// The shared deps filename used by MavenCommandService.
    /// Must match BcdeMvnDependencyFileName from MavenCommandService.
    /// </summary>
    private const string BcdeMvnFileName = "bcde.mvndeps";

    private readonly Mock<IMavenCommandService> mavenCommandServiceMock;
    private readonly Mock<IEnvironmentVariableService> envVarServiceMock;
    private readonly Mock<IFileUtilityService> fileUtilityServiceMock;

    public MvnCliDetectorTests()
    {
        this.mavenCommandServiceMock = new Mock<IMavenCommandService>();
        this.mavenCommandServiceMock.Setup(x => x.BcdeMvnDependencyFileName).Returns(BcdeMvnFileName);

        // Default setup for GenerateDependenciesFileAsync
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        this.DetectorTestUtility.AddServiceMock(this.mavenCommandServiceMock);

        this.envVarServiceMock = new Mock<IEnvironmentVariableService>();
        this.DetectorTestUtility.AddServiceMock(this.envVarServiceMock);

        this.fileUtilityServiceMock = new Mock<IFileUtilityService>();
        this.DetectorTestUtility.AddServiceMock(this.fileUtilityServiceMock);
    }

    [TestMethod]
    public async Task WhenMavenCliNotAvailable_FallsBackToStaticParsing_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>3.12.0</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.GroupId.Should().Be("org.apache.commons");
        mavenComponent.ArtifactId.Should().Be("commons-lang3");
        mavenComponent.Version.Should().Be("3.12.0");
    }

    [TestMethod]
    public async Task WhenMavenCliNotAvailable_DetectsMultipleDependencies_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>3.12.0</version>
        </dependency>
        <dependency>
            <groupId>com.google.guava</groupId>
            <artifactId>guava</artifactId>
            <version>31.1-jre</version>
        </dependency>
        <dependency>
            <groupId>junit</groupId>
            <artifactId>junit</artifactId>
            <version>4.13.2</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        var groupIds = detectedComponents
            .Select(dc => (dc.Component as MavenComponent)?.GroupId)
            .ToList();

        groupIds.Should().Contain("org.apache.commons");
        groupIds.Should().Contain("com.google.guava");
        groupIds.Should().Contain("junit");
    }

    [TestMethod]
    public async Task WhenMavenCliSucceeds_UsesMvnCliResults_Async()
    {
        // Arrange
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";

        this.SetupMvnCliSuccess(componentString);

        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT")));
            });

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.GroupId.Should().Be("org.apache.maven");
        mavenComponent.ArtifactId.Should().Be("maven-compat");
        mavenComponent.Version.Should().Be("3.6.1-SNAPSHOT");
    }

    [TestMethod]
    public async Task WhenMavenCliSucceeds_PreservesTransitiveDependencies_Async()
    {
        // Arrange
        const string rootComponent = "com.test:my-app:jar:1.0.0";
        const string directDependency = "org.apache.commons:commons-lang3:jar:3.12.0";
        const string transitiveDependency = "org.apache.commons:commons-text:jar:1.9";

        var content = $@"{rootComponent}
\- {directDependency}
    \- {transitiveDependency}";

        this.SetupMvnCliSuccess(content);

        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("com.test", "my-app", "1.0.0")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.apache.commons", "commons-lang3", "3.12.0")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.apache.commons", "commons-text", "1.9")),
                    isExplicitReferencedDependency: false,
                    parentComponentId: "org.apache.commons commons-lang3 3.12.0 - Maven");
            });

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        // Verify dependency graph has the transitive relationship
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();
        dependencyGraph.Should().NotBeNull();

        // Verify the transitive component is reachable from the direct dependency
        var directComponentId = "org.apache.commons commons-lang3 3.12.0 - Maven";
        var transitiveComponentId = "org.apache.commons commons-text 1.9 - Maven";

        var directDependencies = dependencyGraph.GetDependenciesForComponent(directComponentId);
        directDependencies.Should().Contain(
            transitiveComponentId,
            "the transitive dependency should be a child of the direct dependency");
    }

    [TestMethod]
    public async Task WhenMavenCliProducesNoOutput_FallsBackToStaticParsing_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // MvnCli runs but produces no bcde.mvndeps files (simulating failure)
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>3.12.0</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.GroupId.Should().Be("org.apache.commons");
        mavenComponent.ArtifactId.Should().Be("commons-lang3");
        mavenComponent.Version.Should().Be("3.12.0");
    }

    [TestMethod]
    public async Task StaticParser_IgnoresDependenciesWithoutVersion_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <!-- No version specified - should be ignored -->
        </dependency>
        <dependency>
            <groupId>com.google.guava</groupId>
            <artifactId>guava</artifactId>
            <version>31.1-jre</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.ArtifactId.Should().Be("guava");
    }

    [TestMethod]
    public async Task StaticParser_IgnoresDependenciesWithVersionRanges_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>[3.0,4.0)</version>
        </dependency>
        <dependency>
            <groupId>com.google.guava</groupId>
            <artifactId>guava</artifactId>
            <version>31.1-jre</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Version ranges with commas are ignored
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.ArtifactId.Should().Be("guava");
    }

    [TestMethod]
    public async Task StaticParser_ResolvesPropertyVersions_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <properties>
        <commons.version>3.12.0</commons.version>
    </properties>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>${commons.version}</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.Version.Should().Be("3.12.0");
    }

    [TestMethod]
    public async Task StaticParser_IgnoresDependenciesWithUnresolvablePropertyVersions_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>${undefined.property}</version>
        </dependency>
        <dependency>
            <groupId>com.google.guava</groupId>
            <artifactId>guava</artifactId>
            <version>31.1-jre</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Unresolvable property versions are ignored
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.ArtifactId.Should().Be("guava");
    }

    [TestMethod]
    public async Task StaticParser_ResolvesVariableFromPreviousFile_Async()
    {
        // Arrange - Test case 1: Variable defined in parent POM, referenced in child POM
        // Uses Maven's standard parent inheritance mechanism
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        // Setup fileUtilityService to allow parent POM resolution
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.Is<string>(s => s.EndsWith("pom.xml") && !s.Contains("module"))))
            .Returns(true);

        var parentPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>parent</artifactId>
    <version>1.0.0</version>
    <packaging>pom</packaging>
    <properties>
        <commons.version>3.12.0</commons.version>
    </properties>
</project>";

        var childPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <parent>
        <groupId>com.test</groupId>
        <artifactId>parent</artifactId>
        <version>1.0.0</version>
    </parent>
    <artifactId>child</artifactId>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>${commons.version}</version>
        </dependency>
    </dependencies>
</project>";

        // Act - Parent processed first, then child
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", parentPomContent)
            .WithFile("module/pom.xml", childPomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.GroupId.Should().Be("org.apache.commons");
        mavenComponent.ArtifactId.Should().Be("commons-lang3");
        mavenComponent.Version.Should().Be("3.12.0");
    }

    [TestMethod]
    public async Task StaticParser_BackfillsVariableFromLaterFile_Async()
    {
        // Arrange - Test case 2: Child processed first, parent processed second (deferred resolution)
        // Tests that variables can be resolved even when parent is processed after child
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        // Setup fileUtilityService to allow parent POM resolution
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.Is<string>(s => s.EndsWith("pom.xml") && !s.Contains("module"))))
            .Returns(true);

        var childPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <parent>
        <groupId>com.test</groupId>
        <artifactId>parent</artifactId>
        <version>1.0.0</version>
    </parent>
    <artifactId>child</artifactId>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>${commons.version}</version>
        </dependency>
    </dependencies>
</project>";

        var parentPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>parent</artifactId>
    <version>1.0.0</version>
    <packaging>pom</packaging>
    <properties>
        <commons.version>3.13.0</commons.version>
    </properties>
</project>";

        // Act - Child processed first (has unresolved variable), then parent
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("module/pom.xml", childPomContent)
            .WithFile("pom.xml", parentPomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.GroupId.Should().Be("org.apache.commons");
        mavenComponent.ArtifactId.Should().Be("commons-lang3");
        mavenComponent.Version.Should().Be("3.13.0");
    }

    [TestMethod]
    public async Task StaticParser_LocalVariableDefinitionTakesPriority_Async()
    {
        // Arrange - Test case 3: Variable defined in both files, local definition has priority
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var firstPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>parent</artifactId>
    <version>1.0.0</version>
    <properties>
        <commons.version>3.11.0</commons.version>
    </properties>
</project>";

        var secondPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>child</artifactId>
    <version>1.0.0</version>
    <properties>
        <commons.version>3.14.0</commons.version>
    </properties>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>${commons.version}</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", firstPomContent)
            .WithFile("module/pom.xml", secondPomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.GroupId.Should().Be("org.apache.commons");
        mavenComponent.ArtifactId.Should().Be("commons-lang3");

        // Should use the local definition (3.14.0) instead of parent definition (3.11.0)
        mavenComponent.Version.Should().Be("3.14.0");
    }

    [TestMethod]
    public async Task StaticParser_OutOfOrderProcessing_RespectsAncestorPriority_Async()
    {
        // Arrange - Test processing order independence for ancestor priority
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        // Setup fileUtilityService to return false for directory traversal
        // This forces the detector to use coordinate-based parent resolution
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.IsAny<string>()))
            .Returns(false);

        var grandparentPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>grandparent</artifactId>
    <version>1.0.0</version>
    <packaging>pom</packaging>
    <properties>
        <commons.version>3.10.0</commons.version>
    </properties>
</project>";

        var parentPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <parent>
        <groupId>com.test</groupId>
        <artifactId>grandparent</artifactId>
        <version>1.0.0</version>
    </parent>
    <artifactId>parent</artifactId>
    <packaging>pom</packaging>
    <properties>
        <commons.version>3.11.0</commons.version>
    </properties>
</project>";

        var childPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <parent>
        <groupId>com.test</groupId>
        <artifactId>parent</artifactId>
        <version>1.0.0</version>
    </parent>
    <artifactId>child</artifactId>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>${commons.version}</version>
        </dependency>
    </dependencies>
</project>";

        // Act - Process files in out-of-order sequence: grandparent → child → parent
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", grandparentPomContent)
            .WithFile("parent/child/pom.xml", childPomContent)
            .WithFile("parent/pom.xml", parentPomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.GroupId.Should().Be("org.apache.commons");
        mavenComponent.ArtifactId.Should().Be("commons-lang3");

        // Should resolve to parent's version (3.11.0) since parent is the closest ancestor to child,
        // NOT grandparent's version (3.10.0), even though grandparent was processed first.
        mavenComponent.Version.Should().Be("3.11.0");
    }

    [TestMethod]
    public async Task WhenNoPomXmlFiles_ReturnsSuccessWithNoComponents_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task WhenPomXmlHasNoDependencies_ReturnsSuccessWithNoComponents_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }

    [TestMethod]
    public async Task WhenDisableMvnCliTrue_UsesStaticParsing_Async()
    {
        // Arrange - DisableMvnCliEnvVar is true (explicitly disable Maven CLI)
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // Disable MvnCli explicitly
        this.envVarServiceMock.Setup(x => x.IsEnvironmentVariableValueTrue(MvnCliComponentDetector.DisableMvnCliEnvVar))
            .Returns(true);

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>3.12.0</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should detect component via static parsing even though Maven CLI is available
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.GroupId.Should().Be("org.apache.commons");
        mavenComponent.ArtifactId.Should().Be("commons-lang3");
        mavenComponent.Version.Should().Be("3.12.0");

        // Verify MavenCLIExistsAsync was never called since we disabled MvnCli
        this.mavenCommandServiceMock.Verify(x => x.MavenCLIExistsAsync(), Times.Never);
    }

    [TestMethod]
    public async Task WhenDisableMvnCliEnvVarIsFalse_UsesMvnCliNormally_Async()
    {
        // Arrange - Maven CLI is available and CD_MAVEN_DISABLE_CLI is false
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";
        const string validPomXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>test-app</artifactId>
    <version>1.0.0</version>
</project>";

        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // Setup GenerateDependenciesFileAsync to return success
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        // Setup file utility to return the deps file content
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.Is<string>(s => s.EndsWith(BcdeMvnFileName))))
            .Returns(true);
        this.fileUtilityServiceMock.Setup(x => x.ReadAllText(It.Is<string>(s => s.EndsWith(BcdeMvnFileName))))
            .Returns(componentString);

        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT")));
            });

        // Set up the environment variable to NOT disable MvnCli (false)
        this.envVarServiceMock.Setup(x => x.IsEnvironmentVariableValueTrue(MvnCliComponentDetector.DisableMvnCliEnvVar))
            .Returns(false);

        // Act
        var (detectorResult, _) = await this.DetectorTestUtility
            .WithFile("pom.xml", validPomXml)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should use MvnCli since CD_MAVEN_DISABLE_CLI is false
        this.mavenCommandServiceMock.Verify(x => x.MavenCLIExistsAsync(), Times.Once);

        // Verify telemetry shows MvnCliOnly detection method
        detectorResult.AdditionalTelemetryDetails.Should().ContainKey("DetectionMethod");
        detectorResult.AdditionalTelemetryDetails["DetectionMethod"].Should().Be("MvnCliOnly");
    }

    [TestMethod]
    public async Task WhenMvnCliSucceeds_NestedPomXmlsAreFilteredOut_Async()
    {
        // Arrange - Maven CLI is available and succeeds.
        // In a multi-module project, only the root pom.xml should be processed by MvnCli.
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // Setup GenerateDependenciesFileAsync to return success
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        // Setup file utility to return the deps file content
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.Is<string>(s => s.EndsWith(BcdeMvnFileName))))
            .Returns(true);
        this.fileUtilityServiceMock.Setup(x => x.ReadAllText(It.Is<string>(s => s.EndsWith(BcdeMvnFileName))))
            .Returns("com.test:parent-app:jar:1.0.0");

        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                // MvnCli processes root and generates deps for all modules
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.projecta", "dep-from-root-a", "1.0.0")));
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.projecta", "dep-from-nested-a", "1.0.0")));
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.projecta", "dep-from-submodule-a", "1.0.0")));
            });

        // Root pom.xml content (doesn't matter for this test, just needs to exist)
        var rootPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <groupId>com.test</groupId>
    <artifactId>parent-app</artifactId>
    <version>1.0.0</version>
    <packaging>pom</packaging>
</project>";

        // Nested module pom.xml content
        var moduleAPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <parent>
        <groupId>com.test</groupId>
        <artifactId>parent-app</artifactId>
        <version>1.0.0</version>
    </parent>
    <artifactId>module-a</artifactId>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>3.12.0</version>
        </dependency>
    </dependencies>
</project>";

        // Act - Add root pom.xml first, then nested module pom.xml
        // The root should get MvnCli bcde.mvndeps, nested should be filtered
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", rootPomContent)
            .WithFile("module-a/pom.xml", moduleAPomContent)
            .WithFile(BcdeMvnFileName, "com.test:parent-app:jar:1.0.0", [BcdeMvnFileName])
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should have components from MvnCli (parent + modules), not from static parsing
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        // MvnCli should only be called once for root pom.xml (nested filtered out)
        this.mavenCommandServiceMock.Verify(
            x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenMvnCliFailsCompletely_AllNestedPomXmlsAreRestoredForStaticParsing_Async()
    {
        // Arrange - Maven CLI is available but fails for all pom.xml files (e.g., auth error)
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // MvnCli runs but produces no bcde.mvndeps files (simulating complete failure)
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        // Root pom.xml content
        var rootPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <groupId>com.test</groupId>
    <artifactId>parent-app</artifactId>
    <version>1.0.0</version>
    <packaging>pom</packaging>
    <dependencies>
        <dependency>
            <groupId>org.springframework</groupId>
            <artifactId>spring-core</artifactId>
            <version>5.3.0</version>
        </dependency>
    </dependencies>
</project>";

        // Nested module pom.xml content - should be restored for static parsing
        var moduleAPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <parent>
        <groupId>com.test</groupId>
        <artifactId>parent-app</artifactId>
        <version>1.0.0</version>
    </parent>
    <artifactId>module-a</artifactId>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>3.12.0</version>
        </dependency>
    </dependencies>
</project>";

        // Another nested module
        var moduleBPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <parent>
        <groupId>com.test</groupId>
        <artifactId>parent-app</artifactId>
        <version>1.0.0</version>
    </parent>
    <artifactId>module-b</artifactId>
    <dependencies>
        <dependency>
            <groupId>com.google.guava</groupId>
            <artifactId>guava</artifactId>
            <version>31.1-jre</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", rootPomContent)
            .WithFile("module-a/pom.xml", moduleAPomContent)
            .WithFile("module-b/pom.xml", moduleBPomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // All pom.xml files should be processed via static parsing (nested poms restored)
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3); // spring-core, commons-lang3, guava

        var artifactIds = detectedComponents
            .Select(dc => (dc.Component as MavenComponent)?.ArtifactId)
            .ToList();

        artifactIds.Should().Contain("spring-core");    // From root pom.xml
        artifactIds.Should().Contain("commons-lang3");  // From module-a/pom.xml (nested - restored)
        artifactIds.Should().Contain("guava");          // From module-b/pom.xml (nested - restored)
    }

    [TestMethod]
    public async Task WhenMvnCliFailsWithAuthError_LogsFailedEndpointAndSetsTelemetry_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // Simulate Maven CLI failure with authentication error message containing endpoint URL
        // The URL intentionally contains userinfo (credentials) and a path so we can verify they are stripped.
        var authErrorMessage = "[ERROR] Failed to execute goal on project my-app: Could not resolve dependencies for project com.test:my-app:jar:1.0.0: " +
            "Failed to collect dependencies at com.private:private-lib:jar:2.0.0: " +
            "Failed to read artifact descriptor for com.private:private-lib:jar:2.0.0: " +
            "Could not transfer artifact com.private:private-lib:pom:2.0.0 from/to private-repo (https://user:s3cr3t@private-maven-repo.example.com/repository/maven-releases/?token=abc): " +
            "status code: 401, reason phrase: Unauthorized";

        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(false, authErrorMessage));

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>3.12.0</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should fall back to static parsing and detect the component
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.ArtifactId.Should().Be("commons-lang3");

        // Verify telemetry contains auth failure info
        detectorResult.AdditionalTelemetryDetails.Should().ContainKey("FallbackReason");
        detectorResult.AdditionalTelemetryDetails["FallbackReason"].Should().Be("AuthenticationFailure");

        // Verify telemetry contains the failed endpoint normalized to scheme+host only.
        // Credentials (userinfo), path, and query string must NOT appear in telemetry.
        detectorResult.AdditionalTelemetryDetails.Should().ContainKey("FailedEndpoints");
        var failedEndpoints = detectorResult.AdditionalTelemetryDetails["FailedEndpoints"];
        failedEndpoints.Should().Be(
            "https://private-maven-repo.example.com",
            "credentials, path, and query string must be stripped before reaching telemetry");
        failedEndpoints.Should().NotContain("user", "userinfo must be stripped");
        failedEndpoints.Should().NotContain("s3cr3t", "credentials must be stripped");
        failedEndpoints.Should().NotContain("token", "query string must be stripped");
        failedEndpoints.Should().NotContain("/repository", "path must be stripped");
    }

    [TestMethod]
    public async Task WhenMvnCliFailsWithNonAuthError_SetsFallbackReasonToOther_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // Simulate Maven CLI failure with a non-auth error (e.g., build error)
        var nonAuthErrorMessage = "[ERROR] Failed to execute goal on project my-app: Compilation failure: " +
            "src/main/java/com/test/App.java:[10,5] cannot find symbol";

        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(false, nonAuthErrorMessage));

        var pomXmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>my-app</artifactId>
    <version>1.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>3.12.0</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should fall back to static parsing
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        // Verify telemetry shows non-auth failure
        detectorResult.AdditionalTelemetryDetails.Should().ContainKey("FallbackReason");
        detectorResult.AdditionalTelemetryDetails["FallbackReason"].Should().Be("OtherMvnCliFailure");

        // Should NOT have FailedEndpoints since this wasn't an auth error
        detectorResult.AdditionalTelemetryDetails.Should().NotContainKey("FailedEndpoints");
    }

    [TestMethod]
    public async Task WhenAuthenticationFailsAndParentChildPropertiesUsed_MaintainsCorrectOrderingDuringFallback_Async()
    {
        // Arrange
        const string parentPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.yammer.veritas</groupId>
    <artifactId>veritas-parent</artifactId>
    <version>1.0-SNAPSHOT</version>
    <packaging>pom</packaging>

    <properties>
        <commons-lang3.version>3.18.0</commons-lang3.version>
        <mockito.version>4.11.0</mockito.version>
        <jackson.version>2.21.1</jackson.version>
    </properties>

    <modules>
        <module>veritas-api</module>
    </modules>

    <repositories>
        <repository>
            <id>yammer-artifacts</id>
            <name>yammer-artifacts</name>
            <url>https://pkgs.dev.azure.com/yammer/_packaging/yammer-artifacts/maven/v1</url>
        </repository>
    </repositories>
</project>";

        const string childPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <parent>
        <artifactId>veritas-parent</artifactId>
        <groupId>com.yammer.veritas</groupId>
        <version>1.0-SNAPSHOT</version>
    </parent>
    <modelVersion>4.0.0</modelVersion>
    <artifactId>veritas-api</artifactId>

    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>${commons-lang3.version}</version>
        </dependency>
        <dependency>
            <groupId>org.mockito</groupId>
            <artifactId>mockito-core</artifactId>
            <version>${mockito.version}</version>
            <scope>test</scope>
        </dependency>
        <dependency>
            <groupId>com.fasterxml.jackson.core</groupId>
            <artifactId>jackson-core</artifactId>
            <version>${jackson.version}</version>
        </dependency>
    </dependencies>
</project>";

        // Setup Maven CLI to fail with authentication error (401 Unauthorized)
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(false, "status code: 401, reason phrase: Unauthorized"));

        // Act - Test with parent and child POM structure
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", parentPomContent)
            .WithFile("veritas-api/pom.xml", childPomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should fall back to static parsing after authentication failure
        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should detect all 3 property-based dependencies from child POM
        detectedComponents.Should().HaveCount(3);

        var mavenComponents = detectedComponents.Where(x => x.Component is MavenComponent).ToList();
        mavenComponents.Should().HaveCount(3);

        // Verify each property-based dependency was resolved with correct version from parent
        var commonsLang3 = mavenComponents.FirstOrDefault(x =>
            ((MavenComponent)x.Component).ArtifactId == "commons-lang3");
        commonsLang3.Should().NotBeNull();
        ((MavenComponent)commonsLang3.Component).Version.Should().Be("3.18.0");

        var mockitoCore = mavenComponents.FirstOrDefault(x =>
            ((MavenComponent)x.Component).ArtifactId == "mockito-core");
        mockitoCore.Should().NotBeNull();
        ((MavenComponent)mockitoCore.Component).Version.Should().Be("4.11.0");

        var jacksonCore = mavenComponents.FirstOrDefault(x =>
            ((MavenComponent)x.Component).ArtifactId == "jackson-core");
        jacksonCore.Should().NotBeNull();
        ((MavenComponent)jacksonCore.Component).Version.Should().Be("2.21.1");

        // Verify telemetry shows authentication failure
        detectorResult.AdditionalTelemetryDetails.Should().ContainKey("FallbackReason");
        detectorResult.AdditionalTelemetryDetails["FallbackReason"].Should().Be("AuthenticationFailure");

        // Should have method showing mixed detection was used (CLI failed but fallback succeeded)
        detectorResult.AdditionalTelemetryDetails.Should().ContainKey("DetectionMethod");
        detectorResult.AdditionalTelemetryDetails["DetectionMethod"].Should().Be("Mixed");
    }

    [TestMethod]
    public async Task VariableResolution_SiblingPomVariablesShouldNotBeUsed_Async()
    {
        // Arrange - Maven-compliant behavior: sibling POM variables should NOT be resolved
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        // Parent POM with a property
        var parentPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>parent</artifactId>
    <version>1.0.0</version>
    <packaging>pom</packaging>
    <properties>
        <commons.version>3.12.0</commons.version>
    </properties>
</project>";

        // Sibling POM with different variable - should NOT be used for resolution
        var siblingPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <parent>
        <groupId>com.test</groupId>
        <artifactId>parent</artifactId>
        <version>1.0.0</version>
    </parent>
    <groupId>com.test</groupId>
    <artifactId>sibling</artifactId>
    <properties>
        <guava.version>31.1-jre</guava.version>
    </properties>
</project>";

        // Target POM trying to use sibling's variable - should fail to resolve
        var targetPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <parent>
        <groupId>com.test</groupId>
        <artifactId>parent</artifactId>
        <version>1.0.0</version>
    </parent>
    <groupId>com.test</groupId>
    <artifactId>target</artifactId>
    <dependencies>
        <dependency>
            <groupId>com.google.guava</groupId>
            <artifactId>guava</artifactId>
            <version>${guava.version}</version>
        </dependency>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>${commons.version}</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("parent/pom.xml", parentPomContent)
            .WithFile("sibling/pom.xml", siblingPomContent)
            .WithFile("target/pom.xml", targetPomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should only resolve commons-lang3 (from parent), not guava (sibling variable)
        detectedComponents.Should().HaveCount(1);
        var component = detectedComponents.First().Component as MavenComponent;
        component.Should().NotBeNull();
        component.GroupId.Should().Be("org.apache.commons");
        component.ArtifactId.Should().Be("commons-lang3");
        component.Version.Should().Be("3.12.0"); // Resolved from parent
    }

    [TestMethod]
    public async Task VariableResolution_ParentHierarchyVariablesShouldBeUsed_Async()
    {
        // Arrange - Maven-compliant behavior: parent/grandparent variables should be resolved
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        // Setup fileUtilityService to allow parent POM resolution
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.IsAny<string>()))
            .Returns((string path) => path.EndsWith("pom.xml"));

        // Grandparent POM
        var grandparentPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>grandparent</artifactId>
    <version>1.0.0</version>
    <packaging>pom</packaging>
    <properties>
        <junit.version>4.13.2</junit.version>
    </properties>
</project>";

        // Parent POM
        var parentPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <parent>
        <groupId>com.test</groupId>
        <artifactId>grandparent</artifactId>
        <version>1.0.0</version>
    </parent>
    <groupId>com.test</groupId>
    <artifactId>parent</artifactId>
    <properties>
        <commons.version>3.12.0</commons.version>
    </properties>
</project>";

        // Child POM using variables from parent hierarchy
        var childPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <parent>
        <groupId>com.test</groupId>
        <artifactId>parent</artifactId>
        <version>1.0.0</version>
    </parent>
    <groupId>com.test</groupId>
    <artifactId>child</artifactId>
    <properties>
        <guava.version>31.1-jre</guava.version>
    </properties>
    <dependencies>
        <dependency>
            <groupId>org.apache.commons</groupId>
            <artifactId>commons-lang3</artifactId>
            <version>${commons.version}</version>
        </dependency>
        <dependency>
            <groupId>junit</groupId>
            <artifactId>junit</artifactId>
            <version>${junit.version}</version>
        </dependency>
        <dependency>
            <groupId>com.google.guava</groupId>
            <artifactId>guava</artifactId>
            <version>${guava.version}</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("grandparent/pom.xml", grandparentPomContent)
            .WithFile("parent/pom.xml", parentPomContent)
            .WithFile("child/pom.xml", childPomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        var components = detectedComponents.Select(x => x.Component as MavenComponent).ToList();

        // Should resolve commons-lang3 from parent
        var commonsComponent = components.FirstOrDefault(c => c.ArtifactId == "commons-lang3");
        commonsComponent.Should().NotBeNull();
        commonsComponent.Version.Should().Be("3.12.0");

        // Should resolve junit from grandparent
        var junitComponent = components.FirstOrDefault(c => c.ArtifactId == "junit");
        junitComponent.Should().NotBeNull();
        junitComponent.Version.Should().Be("4.13.2");

        // Should resolve guava from current POM
        var guavaComponent = components.FirstOrDefault(c => c.ArtifactId == "guava");
        guavaComponent.Should().NotBeNull();
        guavaComponent.Version.Should().Be("31.1-jre");
    }

    [TestMethod]
    public async Task VariableResolution_MavenBuiltInVariablesShouldWork_Async()
    {
        // Arrange - Test Maven built-in variables like ${project.version}
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        this.fileUtilityServiceMock.Setup(x => x.Exists(It.IsAny<string>()))
            .Returns(false);

        var pomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>maven-builtin-test</artifactId>
    <version>1.5.0</version>
    <dependencies>
        <dependency>
            <groupId>com.test</groupId>
            <artifactId>internal-dependency</artifactId>
            <version>${project.version}</version>
        </dependency>
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(1);

        var component = detectedComponents.First().Component as MavenComponent;
        component.Should().NotBeNull();
        component.GroupId.Should().Be("com.test");
        component.ArtifactId.Should().Be("internal-dependency");
        component.Version.Should().Be("1.5.0"); // Should resolve ${project.version}
    }

    [TestMethod]
    public async Task WhenCleanupCreatedFilesIsTrue_DeletesDepsFileAfterProcessing_Async()
    {
        // Arrange
        const string componentString = "org.apache.commons:commons-lang3:jar:3.12.0";
        this.SetupMvnCliSuccess(componentString);

        var deletedFiles = new System.Collections.Generic.List<string>();
        this.fileUtilityServiceMock
            .Setup(x => x.Delete(It.IsAny<string>()))
            .Callback<string>(path => deletedFiles.Add(path));

        // Act: default ScanRequest has cleanupCreatedFiles=true
        var (detectorResult, _) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        deletedFiles.Should().ContainSingle(
            f => f.Contains(BcdeMvnFileName),
            "the deps file should be deleted after its content is consumed");
    }

    [TestMethod]
    public async Task WhenCleanupCreatedFilesIsFalse_DoesNotDeleteDepsFile_Async()
    {
        // Arrange
        const string componentString = "org.apache.commons:commons-lang3:jar:3.12.0";
        this.SetupMvnCliSuccess(componentString);

        var scanRequest = new ScanRequest(
            new System.IO.DirectoryInfo(System.IO.Path.GetTempPath()),
            null,
            null,
            new System.Collections.Generic.Dictionary<string, string>(),
            null,
            new Microsoft.ComponentDetection.Common.DependencyGraph.ComponentRecorder(),
            cleanupCreatedFiles: false);

        // Act
        var (detectorResult, _) = await this.DetectorTestUtility
            .WithScanRequest(scanRequest)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        this.fileUtilityServiceMock.Verify(
            x => x.Delete(It.IsAny<string>()),
            Times.Never,
            "the deps file should not be deleted when CleanupCreatedFiles is false");
    }

    [TestMethod]
    public async Task TestSmartLoopPreventionInDirectoryTraversal()
    {
        // Arrange - Setup Maven CLI to fail so we use static parser
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        // Create a child POM that references a parent that won't be found in directory traversal
        var childPomContent = """
            <project xmlns="http://maven.apache.org/POM/4.0.0">
                <modelVersion>4.0.0</modelVersion>
                <parent>
                    <groupId>com.example</groupId>
                    <artifactId>parent-project</artifactId>
                    <version>1.0.0</version>
                </parent>
                <groupId>com.example</groupId>
                <artifactId>child-project</artifactId>
                <version>${parent.version}</version>
                <dependencies>
                    <dependency>
                        <groupId>junit</groupId>
                        <artifactId>junit</artifactId>
                        <version>4.13.2</version>
                    </dependency>
                </dependencies>
            </project>
            """;

        // Act & Assert - This should not hang or throw due to infinite directory traversal
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", childPomContent)
            .ExecuteDetectorAsync();

        // Should complete successfully without infinite loops
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should register the dependency with direct version (junit)
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(1);

        var component = detectedComponents.First().Component as MavenComponent;
        component.Should().NotBeNull();
        component.GroupId.Should().Be("junit");
        component.ArtifactId.Should().Be("junit");
        component.Version.Should().Be("4.13.2");
    }

    [TestMethod]
    public async Task TestPerformanceOfSmartLoopPrevention()
    {
        // Arrange - Setup static parsing mode
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var pom1Content = CreatePomWithParentReference("project1", "parent-project", "1.0.0");
        var pom2Content = CreatePomWithParentReference("project2", "parent-project", "1.0.0");
        var pom3Content = CreatePomWithParentReference("project3", "parent-project", "1.0.0");

        // Act - Process multiple POMs (test validates completion, not timing)
        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("project1/pom.xml", pom1Content)
            .WithFile("project2/pom.xml", pom2Content)
            .WithFile("project3/pom.xml", pom3Content)
            .ExecuteDetectorAsync();

        // Assert - Should complete without hanging and produce correct results
        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should have detected direct dependencies from all 3 POMs
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
    }

    private static string CreatePomWithParentReference(string artifactId, string parentArtifactId, string parentVersion)
    {
        return $"""
            <project xmlns="http://maven.apache.org/POM/4.0.0">
                <modelVersion>4.0.0</modelVersion>
                <parent>
                    <groupId>com.example</groupId>
                    <artifactId>{parentArtifactId}</artifactId>
                    <version>{parentVersion}</version>
                </parent>
                <artifactId>{artifactId}</artifactId>
                <dependencies>
                    <dependency>
                        <groupId>com.example</groupId>
                        <artifactId>{artifactId}-dependency</artifactId>
                        <version>2.0.0</version>
                    </dependency>
                </dependencies>
            </project>
            """;
    }

    private void SetupMvnCliSuccess(string depsFileContent)
    {
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        this.mavenCommandServiceMock.Setup(x => x.BcdeMvnDependencyFileName)
            .Returns(BcdeMvnFileName);

        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        // Setup file utility service to return the deps file content
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.Is<string>(s => s.EndsWith(BcdeMvnFileName))))
            .Returns(true);
        this.fileUtilityServiceMock.Setup(x => x.ReadAllText(It.Is<string>(s => s.EndsWith(BcdeMvnFileName))))
            .Returns(depsFileContent);

        const string validPomXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <modelVersion>4.0.0</modelVersion>
    <groupId>com.test</groupId>
    <artifactId>test-app</artifactId>
    <version>1.0.0</version>
</project>";

        this.DetectorTestUtility.WithFile("pom.xml", validPomXml);

        // Add the dependency file that Maven CLI would have generated
        this.DetectorTestUtility.WithFile(BcdeMvnFileName, depsFileContent, [BcdeMvnFileName]);
    }
}
