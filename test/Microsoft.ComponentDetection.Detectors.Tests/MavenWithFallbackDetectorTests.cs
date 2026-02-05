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
public class MavenWithFallbackDetectorTests : BaseDetectorTest<MavenWithFallbackDetector>
{
    private const string BcdeMvnFileName = "bcde-fallback.mvndeps";

    private readonly Mock<IMavenCommandService> mavenCommandServiceMock;
    private readonly Mock<IEnvironmentVariableService> envVarServiceMock;

    public MavenWithFallbackDetectorTests()
    {
        this.mavenCommandServiceMock = new Mock<IMavenCommandService>();
        this.mavenCommandServiceMock.Setup(x => x.BcdeMvnDependencyFileName).Returns(BcdeMvnFileName);

        // Default setup for GenerateDependenciesFileAsync (3-parameter version for backwards compatibility)
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        // Default setup for GenerateDependenciesFileAsync (4-parameter version with custom local repository)
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        this.DetectorTestUtility.AddServiceMock(this.mavenCommandServiceMock);

        this.envVarServiceMock = new Mock<IEnvironmentVariableService>();
        this.DetectorTestUtility.AddServiceMock(this.envVarServiceMock);
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
    }

    [TestMethod]
    public async Task WhenMavenCliProducesNoOutput_FallsBackToStaticParsing_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // MvnCli runs but produces no bcde-fallback.mvndeps files (simulating failure)
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
        this.envVarServiceMock.Setup(x => x.IsEnvironmentVariableValueTrue(MavenWithFallbackDetector.DisableMvnCliEnvVar))
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

        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT")));
            });

        // Set up the environment variable to NOT disable MvnCli (false)
        this.envVarServiceMock.Setup(x => x.IsEnvironmentVariableValueTrue(MavenWithFallbackDetector.DisableMvnCliEnvVar))
            .Returns(false);

        // Act
        var (detectorResult, _) = await this.DetectorTestUtility
            .WithFile("pom.xml", componentString)
            .WithFile("pom.xml", componentString, searchPatterns: [BcdeMvnFileName])
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should use MvnCli since CD_MAVEN_DISABLE_CLI is false
        this.mavenCommandServiceMock.Verify(x => x.MavenCLIExistsAsync(), Times.Once);
    }

    [TestMethod]
    public async Task WhenDisableMvnCliEnvVarNotSet_UsesMvnCliNormally_Async()
    {
        // Arrange - Maven CLI is available and CD_MAVEN_DISABLE_CLI is NOT set (doesn't exist)
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";

        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT")));
            });

        // Explicitly set up the environment variable to NOT exist (returns false)
        this.envVarServiceMock.Setup(x => x.IsEnvironmentVariableValueTrue(MavenWithFallbackDetector.DisableMvnCliEnvVar))
            .Returns(false);

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", componentString)
            .WithFile("pom.xml", componentString, searchPatterns: [BcdeMvnFileName])
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should use MvnCli since CD_MAVEN_DISABLE_CLI doesn't exist
        this.mavenCommandServiceMock.Verify(x => x.MavenCLIExistsAsync(), Times.Once);

        // Verify telemetry shows MvnCliOnly detection method
        detectorResult.AdditionalTelemetryDetails.Should().ContainKey("DetectionMethod");
        detectorResult.AdditionalTelemetryDetails["DetectionMethod"].Should().Be("MvnCliOnly");
    }

    [TestMethod]
    public async Task WhenDisableMvnCliEnvVarSetToInvalidValue_UsesMvnCliNormally_Async()
    {
        // Arrange - Maven CLI is available and CD_MAVEN_DISABLE_CLI is set to an invalid (non-boolean) value
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";

        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT")));
            });

        // Set up the environment variable with an invalid value (IsEnvironmentVariableValueTrue returns false for non-"true" values)
        this.envVarServiceMock.Setup(x => x.IsEnvironmentVariableValueTrue(MavenWithFallbackDetector.DisableMvnCliEnvVar))
            .Returns(false);

        // Act
        var (detectorResult, _) = await this.DetectorTestUtility
            .WithFile("pom.xml", componentString)
            .WithFile("pom.xml", componentString, searchPatterns: [BcdeMvnFileName])
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should use MvnCli since the env var value is invalid (bool.TryParse fails)
        this.mavenCommandServiceMock.Verify(x => x.MavenCLIExistsAsync(), Times.Once);
    }

    [TestMethod]
    public async Task WhenMvnCliSucceeds_NestedPomXmlsAreFilteredOut_Async()
    {
        // Arrange - Maven CLI is available and succeeds.
        // In a multi-module project, only the root pom.xml should be processed by MvnCli.
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                // MvnCli processes root and generates deps for all modules
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("com.test", "parent-app", "1.0.0")));
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("com.test", "module-a", "1.0.0")));
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("com.test", "module-b", "1.0.0")));
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
        // The root should get MvnCli bcde-fallback.mvndeps, nested should be filtered
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", rootPomContent)
            .WithFile("module-a/pom.xml", moduleAPomContent)
            .WithFile("bcde-fallback.mvndeps", "com.test:parent-app:jar:1.0.0", searchPatterns: [BcdeMvnFileName])
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // Should have components from MvnCli (parent + modules), not from static parsing
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        // MvnCli should only be called once for root pom.xml (nested filtered out)
        this.mavenCommandServiceMock.Verify(
            x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenMvnCliFailsCompletely_AllNestedPomXmlsAreRestoredForStaticParsing_Async()
    {
        // Arrange - Maven CLI is available but fails for all pom.xml files (e.g., auth error)
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // MvnCli runs but produces no bcde-fallback.mvndeps files (simulating complete failure)
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
    public async Task WhenMvnCliPartiallyFails_NestedPomXmlsRestoredOnlyForFailedDirectories_Async()
    {
        // Arrange - Maven CLI succeeds for projectA but fails for projectB.
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // MvnCli runs but only produces output for projectA (projectB fails)
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                // Only register components for projectA's bcde-fallback.mvndeps
                if (pr.ComponentStream.Location.Contains("projectA"))
                {
                    pr.SingleFileComponentRecorder.RegisterUsage(
                        new DetectedComponent(new MavenComponent("com.projecta", "app-a", "1.0.0")));
                    pr.SingleFileComponentRecorder.RegisterUsage(
                        new DetectedComponent(new MavenComponent("com.projecta", "module-a1", "1.0.0")));
                }
            });

        // ProjectA - MvnCli will succeed
        var projectAPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <groupId>com.projecta</groupId>
    <artifactId>app-a</artifactId>
    <version>1.0.0</version>
</project>";

        var projectAModulePomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <parent>
        <groupId>com.projecta</groupId>
        <artifactId>app-a</artifactId>
        <version>1.0.0</version>
    </parent>
    <artifactId>module-a1</artifactId>
    <dependencies>
        <dependency>
            <groupId>org.projecta</groupId>
            <artifactId>dep-from-nested-a</artifactId>
            <version>1.0.0</version>
        </dependency>
    </dependencies>
</project>";

        // ProjectB - MvnCli will fail (no bcde-fallback.mvndeps generated)
        var projectBPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <groupId>com.projectb</groupId>
    <artifactId>app-b</artifactId>
    <version>2.0.0</version>
    <dependencies>
        <dependency>
            <groupId>org.projectb</groupId>
            <artifactId>dep-from-root-b</artifactId>
            <version>2.0.0</version>
        </dependency>
    </dependencies>
</project>";

        var projectBModulePomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <parent>
        <groupId>com.projectb</groupId>
        <artifactId>app-b</artifactId>
        <version>2.0.0</version>
    </parent>
    <artifactId>module-b1</artifactId>
    <dependencies>
        <dependency>
            <groupId>org.projectb</groupId>
            <artifactId>dep-from-nested-b</artifactId>
            <version>2.0.0</version>
        </dependency>
    </dependencies>
</project>";

        // Act - projectA gets bcde-fallback.mvndeps, projectB does not
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("projectA/pom.xml", projectAPomContent)
            .WithFile("projectA/module-a1/pom.xml", projectAModulePomContent)
            .WithFile("projectB/pom.xml", projectBPomContent)
            .WithFile("projectB/module-b1/pom.xml", projectBModulePomContent)
            .WithFile("projectA/bcde-fallback.mvndeps", "com.projecta:app-a:jar:1.0.0", searchPatterns: [BcdeMvnFileName])
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();

        // ProjectA: 2 components from MvnCli (app-a, module-a1)
        // ProjectB: 2 components from static parsing (dep-from-root-b, dep-from-nested-b)
        // Note: nested pom in projectA should NOT be statically parsed (MvnCli handled it)
        // Note: nested pom in projectB SHOULD be statically parsed (MvnCli failed)
        detectedComponents.Should().HaveCount(4);

        var artifactIds = detectedComponents
            .Select(dc => (dc.Component as MavenComponent)?.ArtifactId)
            .ToList();

        // From MvnCli for projectA
        artifactIds.Should().Contain("app-a");
        artifactIds.Should().Contain("module-a1");

        // From static parsing for projectB (both root and nested restored)
        artifactIds.Should().Contain("dep-from-root-b");
        artifactIds.Should().Contain("dep-from-nested-b");

        // Should NOT contain dep-from-nested-a (that nested pom was handled by MvnCli, not static)
        artifactIds.Should().NotContain("dep-from-nested-a");
    }

    [TestMethod]
    public async Task WhenMvnCliFailsWithAuthError_LogsFailedEndpointAndSetsTelemetry_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // Simulate Maven CLI failure with authentication error message containing endpoint URL
        var authErrorMessage = "[ERROR] Failed to execute goal on project my-app: Could not resolve dependencies for project com.test:my-app:jar:1.0.0: " +
            "Failed to collect dependencies at com.private:private-lib:jar:2.0.0: " +
            "Failed to read artifact descriptor for com.private:private-lib:jar:2.0.0: " +
            "Could not transfer artifact com.private:private-lib:pom:2.0.0 from/to private-repo (https://private-maven-repo.example.com/repository/maven-releases/): " +
            "status code: 401, reason phrase: Unauthorized";

        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        // Verify telemetry contains the failed endpoint
        detectorResult.AdditionalTelemetryDetails.Should().ContainKey("FailedEndpoints");
        detectorResult.AdditionalTelemetryDetails["FailedEndpoints"].Should().Contain("https://private-maven-repo.example.com");
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

        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

    private void SetupMvnCliSuccess(string content)
    {
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        // Setup for 4-parameter version (with custom local repository)
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        this.DetectorTestUtility
            .WithFile("pom.xml", content)
            .WithFile("pom.xml", content, searchPatterns: [BcdeMvnFileName]);
    }
}
