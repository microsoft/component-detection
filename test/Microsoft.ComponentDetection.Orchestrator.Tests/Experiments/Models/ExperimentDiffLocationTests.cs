namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments.Models;

using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ExperimentDiffLocationTests
{
    [TestMethod]
    public void ExperimentDiff_TracksLocationChanges_WhenLocationsDiffer()
    {
        // Arrange - Control detector finds component at 3 locations
        var controlComponent = new ScannedComponent
        {
            Component = new NuGetComponent("Newtonsoft.Json", "13.0.1"),
            LocationsFoundAt =
            [
                "src/Project1/packages.config",
                "src/Project2/packages.config",
                "src/Project3/packages.config",
            ],
        };

        // Arrange - Experiment detector finds same component at only 2 locations (from central management)
        var experimentComponent = new ScannedComponent
        {
            Component = new NuGetComponent("Newtonsoft.Json", "13.0.1"),
            LocationsFoundAt =
            [
                "Directory.Packages.props",
                "src/packages.props",
            ],
        };

        var controlComponents = new[] { new ExperimentComponent(controlComponent) };
        var experimentComponents = new[] { new ExperimentComponent(experimentComponent) };

        // Act
        var diff = new ExperimentDiff(controlComponents, experimentComponents);

        // Assert
        diff.LocationChanges.Should().ContainKey("Newtonsoft.Json 13.0.1 - NuGet");
        var locationChange = diff.LocationChanges["Newtonsoft.Json 13.0.1 - NuGet"];

        locationChange.ControlLocationCount.Should().Be(3);
        locationChange.ExperimentLocationCount.Should().Be(2);
        locationChange.LocationCountDelta.Should().Be(-1); // Experiment found 1 fewer location

        locationChange.ControlLocations.Should().Contain("src/Project1/packages.config");
        locationChange.ControlLocations.Should().Contain("src/Project2/packages.config");
        locationChange.ControlLocations.Should().Contain("src/Project3/packages.config");

        locationChange.ExperimentLocations.Should().Contain("Directory.Packages.props");
        locationChange.ExperimentLocations.Should().Contain("src/packages.props");

        locationChange.AddedLocations.Should().Contain("Directory.Packages.props");
        locationChange.AddedLocations.Should().Contain("src/packages.props");

        locationChange.RemovedLocations.Should().Contain("src/Project1/packages.config");
        locationChange.RemovedLocations.Should().Contain("src/Project2/packages.config");
        locationChange.RemovedLocations.Should().Contain("src/Project3/packages.config");
    }

    [TestMethod]
    public void ExperimentDiff_TracksLocationChanges_WhenExperimentFindsMoreLocations()
    {
        // Arrange - Control detector finds component at 2 locations
        var controlComponent = new ScannedComponent
        {
            Component = new NuGetComponent("Microsoft.Extensions.Logging", "7.0.0"),
            LocationsFoundAt =
            [
                "src/Project1/Project1.csproj",
                "src/Project2/Project2.csproj",
            ],
        };

        // Arrange - Experiment detector finds same component at 4 locations
        var experimentComponent = new ScannedComponent
        {
            Component = new NuGetComponent("Microsoft.Extensions.Logging", "7.0.0"),
            LocationsFoundAt =
            [
                "src/Project1/Project1.csproj",
                "src/Project2/Project2.csproj",
                "src/Project3/Project3.csproj",
                "src/Project4/Project4.csproj",
            ],
        };

        var controlComponents = new[] { new ExperimentComponent(controlComponent) };
        var experimentComponents = new[] { new ExperimentComponent(experimentComponent) };

        // Act
        var diff = new ExperimentDiff(controlComponents, experimentComponents);

        // Assert
        diff.LocationChanges.Should().ContainKey("Microsoft.Extensions.Logging 7.0.0 - NuGet");
        var locationChange = diff.LocationChanges["Microsoft.Extensions.Logging 7.0.0 - NuGet"];

        locationChange.ControlLocationCount.Should().Be(2);
        locationChange.ExperimentLocationCount.Should().Be(4);
        locationChange.LocationCountDelta.Should().Be(2); // Experiment found 2 more locations

        locationChange.AddedLocations.Should().Contain("src/Project3/Project3.csproj");
        locationChange.AddedLocations.Should().Contain("src/Project4/Project4.csproj");
        locationChange.AddedLocations.Should().HaveCount(2);

        locationChange.RemovedLocations.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentDiff_TracksLocationChanges_ForNewlyDetectedComponents()
    {
        // Arrange - Control detector doesn't find the component
        var controlComponents = System.Array.Empty<ExperimentComponent>();

        // Arrange - Experiment detector finds the component at 2 locations
        var experimentComponent = new ScannedComponent
        {
            Component = new NuGetComponent("NewPackage", "1.0.0"),
            LocationsFoundAt =
            [
                "Directory.Packages.props",
                "src/packages.props",
            ],
        };

        var experimentComponents = new[] { new ExperimentComponent(experimentComponent) };

        // Act
        var diff = new ExperimentDiff(controlComponents, experimentComponents);

        // Assert
        diff.LocationChanges.Should().ContainKey("NewPackage 1.0.0 - NuGet");
        var locationChange = diff.LocationChanges["NewPackage 1.0.0 - NuGet"];

        locationChange.ControlLocationCount.Should().Be(0);
        locationChange.ExperimentLocationCount.Should().Be(2);
        locationChange.LocationCountDelta.Should().Be(2);

        locationChange.ControlLocations.Should().BeEmpty();
        locationChange.ExperimentLocations.Should().HaveCount(2);
        locationChange.AddedLocations.Should().HaveCount(2);
        locationChange.RemovedLocations.Should().BeEmpty();
    }

    [TestMethod]
    public void ExperimentDiff_TracksLocationChanges_ForRemovedComponents()
    {
        // Arrange - Control detector finds the component at 3 locations
        var controlComponent = new ScannedComponent
        {
            Component = new NuGetComponent("OldPackage", "1.0.0"),
            LocationsFoundAt =
            [
                "src/Project1/packages.config",
                "src/Project2/packages.config",
                "src/Project3/packages.config",
            ],
        };

        var controlComponents = new[] { new ExperimentComponent(controlComponent) };

        // Arrange - Experiment detector doesn't find the component
        var experimentComponents = System.Array.Empty<ExperimentComponent>();

        // Act
        var diff = new ExperimentDiff(controlComponents, experimentComponents);

        // Assert
        diff.LocationChanges.Should().ContainKey("OldPackage 1.0.0 - NuGet");
        var locationChange = diff.LocationChanges["OldPackage 1.0.0 - NuGet"];

        locationChange.ControlLocationCount.Should().Be(3);
        locationChange.ExperimentLocationCount.Should().Be(0);
        locationChange.LocationCountDelta.Should().Be(-3);

        locationChange.ControlLocations.Should().HaveCount(3);
        locationChange.ExperimentLocations.Should().BeEmpty();
        locationChange.AddedLocations.Should().BeEmpty();
        locationChange.RemovedLocations.Should().HaveCount(3);
    }

    [TestMethod]
    public void ExperimentDiff_DoesNotTrackLocationChanges_WhenLocationsAreIdentical()
    {
        // Arrange - Both detectors find component at same locations
        var controlComponent = new ScannedComponent
        {
            Component = new NuGetComponent("SamePackage", "1.0.0"),
            LocationsFoundAt =
            [
                "src/Project1/packages.config",
                "src/Project2/packages.config",
            ],
        };

        var experimentComponent = new ScannedComponent
        {
            Component = new NuGetComponent("SamePackage", "1.0.0"),
            LocationsFoundAt =
            [
                "src/Project1/packages.config",
                "src/Project2/packages.config",
            ],
        };

        var controlComponents = new[] { new ExperimentComponent(controlComponent) };
        var experimentComponents = new[] { new ExperimentComponent(experimentComponent) };

        // Act
        var diff = new ExperimentDiff(controlComponents, experimentComponents);

        // Assert - No location changes should be tracked when locations are identical
        diff.LocationChanges.Should().BeEmpty();
    }
}
