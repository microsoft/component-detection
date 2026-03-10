#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Maven;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class MvnCliDetectorTests : BaseDetectorTest<MvnCliComponentDetector>
{
    private readonly Mock<IMavenCommandService> mavenCommandServiceMock;
    private readonly Mock<IEnvironmentVariableService> environmentVariableServiceMock;
    private readonly Mock<IFileUtilityService> fileUtilityServiceMock;

    public MvnCliDetectorTests()
    {
        this.mavenCommandServiceMock = new Mock<IMavenCommandService>();
        this.environmentVariableServiceMock = new Mock<IEnvironmentVariableService>();
        this.fileUtilityServiceMock = new Mock<IFileUtilityService>();

        this.DetectorTestUtility.AddServiceMock(this.mavenCommandServiceMock);
        this.DetectorTestUtility.AddServiceMock(this.environmentVariableServiceMock);
        this.DetectorTestUtility.AddServiceMock(this.fileUtilityServiceMock);
    }

    [TestMethod]
    public async Task IfMavenIsNotAvailableThenExitDetectorGracefullyAsync()
    {
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(false);

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .ExecuteDetectorAsync();

        componentRecorder.GetDetectedComponents().Should().BeEmpty();
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);
    }

    [TestMethod]
    public async Task MavenAvailableHappyPathAsync()
    {
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";

        this.MvnCliHappyPath(content: componentString);
        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) => pr.SingleFileComponentRecorder.RegisterUsage(new DetectedComponent(new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT"))));
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        var splitComponent = componentString.Split(':');
        splitComponent.Should().HaveElementAt(0, mavenComponent.GroupId);
        splitComponent.Should().HaveElementAt(1, mavenComponent.ArtifactId);
        splitComponent.Should().HaveElementAt(3, mavenComponent.Version);
        mavenComponent.Type.Should().Be(ComponentType.Maven);
    }

    [TestMethod]
    public async Task MavenCli_FileObservableIsNotPresent_DetectionShouldNotFailAsync()
    {
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);

        Func<Task> action = async () => await this.DetectorTestUtility.ExecuteDetectorAsync();

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task MavenRootsAsync()
    {
        const string componentString = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";
        const string childComponentString = "org.apache.maven:maven-compat-child:jar:3.6.1-SNAPSHOT";

        var content = $@"com.bcde.test:top-level:jar:1.0.0{Environment.NewLine}\- {componentString}{Environment.NewLine} \- {childComponentString}";

        this.MvnCliHappyPath(content);
        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("com.bcde.test", "top-levelt", "1.0.0")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat-child", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: false,
                    parentComponentId: "org.apache.maven maven-compat 3.6.1-SNAPSHOT - Maven");
            });

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var splitComponent = componentString.Split(':');
        var splitChildComponent = childComponentString.Split(':');

        var mavenComponent = detectedComponents.FirstOrDefault(x => (x.Component as MavenComponent).ArtifactId == splitChildComponent[1]);
        mavenComponent.Should().NotBeNull();

        componentRecorder.AssertAllExplicitlyReferencedComponents<MavenComponent>(
            mavenComponent.Component.Id,
            parentComponent => parentComponent.ArtifactId == splitComponent[1]);
    }

    [TestMethod]
    public async Task MavenDependencyGraphAsync()
    {
        const string explicitReferencedComponent = "org.apache.maven:maven-compat:jar:3.6.1-SNAPSHOT";

        const string intermediateParentComponent = "org.apache.maven:maven-compat-parent:jar:3.6.1-SNAPSHOT";

        const string leafComponentString = "org.apache.maven:maven-compat-child:jar:3.6.1-SNAPSHOT";

        var content = $@"com.bcde.test:top-level:jar:1.0.0
\- {explicitReferencedComponent}
    \- {intermediateParentComponent}
        \-{leafComponentString}";

        const string explicitReferencedComponentId = "org.apache.maven maven-compat 3.6.1-SNAPSHOT - Maven";
        const string intermediateParentComponentId = "org.apache.maven maven-compat-parent 3.6.1-SNAPSHOT - Maven";
        const string leafComponentId = "org.apache.maven maven-compat-child 3.6.1-SNAPSHOT - Maven";

        this.MvnCliHappyPath(content);
        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(It.IsAny<ProcessRequest>()))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("com.bcde.test", "top-levelt", "1.0.0")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: true);
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat-parent", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: false,
                    parentComponentId: "org.apache.maven maven-compat 3.6.1-SNAPSHOT - Maven");
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(
                        new MavenComponent("org.apache.maven", "maven-compat-child", "3.6.1-SNAPSHOT")),
                    isExplicitReferencedDependency: false,
                    parentComponentId: "org.apache.maven maven-compat-parent 3.6.1-SNAPSHOT - Maven");
            });

        var (detectorResult, componentRecorder) = await this.DetectorTestUtility.ExecuteDetectorAsync();

        componentRecorder.GetDetectedComponents().Should().HaveCount(4);
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        // There is only one graph
        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        dependencyGraph.GetDependenciesForComponent(explicitReferencedComponentId).Should().ContainSingle();
        dependencyGraph.GetDependenciesForComponent(explicitReferencedComponentId).Should().Contain(intermediateParentComponentId);
        dependencyGraph.IsComponentExplicitlyReferenced(explicitReferencedComponentId).Should().BeTrue();

        dependencyGraph.GetDependenciesForComponent(intermediateParentComponentId).Should().ContainSingle();
        dependencyGraph.GetDependenciesForComponent(intermediateParentComponentId).Should().Contain(leafComponentId);
        dependencyGraph.IsComponentExplicitlyReferenced(intermediateParentComponentId).Should().BeFalse();

        dependencyGraph.GetDependenciesForComponent(leafComponentId).Should().BeEmpty();
        dependencyGraph.IsComponentExplicitlyReferenced(leafComponentId).Should().BeFalse();
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
    public async Task WhenMavenCliProducesNoOutput_FallsBackToStaticParsing_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);
        this.mavenCommandServiceMock.Setup(x => x.BcdeMvnDependencyFileName)
            .Returns("bcde.mvndeps");
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        // File exists but is empty
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        this.fileUtilityServiceMock.Setup(x => x.ReadAllText(It.IsAny<string>())).Returns(string.Empty);

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
        detectedComponents.Should().BeEmpty();
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
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
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
        mavenComponent.GroupId.Should().Be("org.apache.commons");
        mavenComponent.ArtifactId.Should().Be("commons-lang3");
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
    </dependencies>
</project>";

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", pomXmlContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task WhenNoPomXmlFiles_ReturnsSuccessWithNoComponents_Async()
    {
        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
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
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task WhenDisableMvnCliTrue_UsesStaticParsing_Async()
    {
        // Arrange
        this.environmentVariableServiceMock.Setup(x => x.IsEnvironmentVariableValueTrue("CD_MAVEN_DISABLE_CLI"))
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

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.GroupId.Should().Be("org.apache.commons");
        mavenComponent.ArtifactId.Should().Be("commons-lang3");
        mavenComponent.Version.Should().Be("3.12.0");

        // Maven CLI should not have been called
        this.mavenCommandServiceMock.Verify(x => x.MavenCLIExistsAsync(), Times.Never);
    }

    [TestMethod]
    public async Task WhenMvnCliFailsWithAuthError_FallsBackToStaticParsingAndSetsTelemetry_Async()
    {
        // Arrange
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);
        this.mavenCommandServiceMock.Setup(x => x.BcdeMvnDependencyFileName)
            .Returns("bcde.mvndeps");

        // Simulate Maven CLI failure with authentication error message containing endpoint URL
        var authErrorMessage = "[ERROR] Failed to execute goal on project my-app: Could not resolve dependencies for project com.test:my-app:jar:1.0.0: " +
            "Failed to collect dependencies at com.private:private-lib:jar:2.0.0: " +
            "Failed to read artifact descriptor for com.private:private-lib:jar:2.0.0: " +
            "Could not transfer artifact com.private:private-lib:pom:2.0.0 from/to private-repo (https://private-maven-repo.example.com/repository/maven-releases/): " +
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
        this.mavenCommandServiceMock.Setup(x => x.BcdeMvnDependencyFileName)
            .Returns("bcde.mvndeps");

        // Simulate Maven CLI failure with non-auth error
        var nonAuthErrorMessage = "[ERROR] Failed to execute goal on project my-app: Compilation failure";

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

        // Should fall back to static parsing and detect the component
        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().ContainSingle();

        // Verify telemetry shows fallback reason as other
        detectorResult.AdditionalTelemetryDetails.Should().ContainKey("FallbackReason");
        detectorResult.AdditionalTelemetryDetails["FallbackReason"].Should().Be("OtherMvnCliFailure");
    }

    [TestMethod]
    public async Task WhenMvnCliSucceeds_NestedPomXmlsAreFilteredOut_Async()
    {
        // Arrange - Maven CLI is available and succeeds for root pom.xml
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);
        this.mavenCommandServiceMock.Setup(x => x.BcdeMvnDependencyFileName)
            .Returns("bcde.mvndeps");

        // Setup GenerateDependenciesFileAsync to return success only for root
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(
            It.Is<ProcessRequest>(pr => pr.ComponentStream.Location.EndsWith("pom.xml")),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));

        // Setup file utility to return the deps file content for root
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.Is<string>(s => s.EndsWith("bcde.mvndeps") && !s.Contains("module"))))
            .Returns(true);
        this.fileUtilityServiceMock.Setup(x => x.ReadAllText(It.Is<string>(s => s.EndsWith("bcde.mvndeps") && !s.Contains("module"))))
            .Returns("com.test:parent-app:jar:1.0.0");

        // Mock ParseDependenciesFile for the root deps file
        this.mavenCommandServiceMock.Setup(x => x.ParseDependenciesFile(
            It.Is<ProcessRequest>(pr => pr.ComponentStream.Location.EndsWith("bcde.mvndeps"))))
            .Callback((ProcessRequest pr) =>
            {
                pr.SingleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(new MavenComponent("com.test", "parent-app", "1.0.0")));
            });

        // Root pom.xml content
        var rootPomContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<project xmlns=""http://maven.apache.org/POM/4.0.0"">
    <groupId>com.test</groupId>
    <artifactId>parent-app</artifactId>
    <version>1.0.0</version>
    <packaging>pom</packaging>
</project>";

        // Nested module pom.xml content that should be ignored since MvnCli succeeded for root
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

        // Act
        var (detectorResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("pom.xml", rootPomContent)
            .WithFile("module-a/pom.xml", moduleAPomContent)
            .ExecuteDetectorAsync();

        // Assert
        detectorResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();

        // Should only contain the component from MvnCli (root), not from static parsing of nested pom
        detectedComponents.Should().ContainSingle();

        var mavenComponent = detectedComponents.First().Component as MavenComponent;
        mavenComponent.Should().NotBeNull();
        mavenComponent.ArtifactId.Should().Be("parent-app");

        // Verify MvnCli was called only once (for root pom.xml)
        this.mavenCommandServiceMock.Verify(
            x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    protected bool ShouldBeEquivalentTo<T>(IEnumerable<T> result, IEnumerable<T> expected)
    {
        result.Should().BeEquivalentTo(expected);
        return true;
    }

    private void MvnCliHappyPath(string content)
    {
        const string bcdeMvnFileName = "bcde.mvndeps";

        this.mavenCommandServiceMock.Setup(x => x.BcdeMvnDependencyFileName)
            .Returns(bcdeMvnFileName);
        this.mavenCommandServiceMock.Setup(x => x.MavenCLIExistsAsync())
            .ReturnsAsync(true);
        this.mavenCommandServiceMock.Setup(x => x.GenerateDependenciesFileAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MavenCliResult(true, null));
        this.fileUtilityServiceMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        this.fileUtilityServiceMock.Setup(x => x.ReadAllText(It.IsAny<string>())).Returns(content);
        this.DetectorTestUtility.WithFile("pom.xml", content)
            .WithFile("pom.xml", content, searchPatterns: [bcdeMvnFileName]);
    }
}
