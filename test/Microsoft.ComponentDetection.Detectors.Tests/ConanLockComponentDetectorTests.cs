#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Conan;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ConanLockComponentDetectorTests : BaseDetectorTest<ConanLockComponentDetector>
{
    private readonly string testConanLockString = @"{
 ""graph_lock"": {
  ""nodes"": {
   ""0"": {
    ""ref"": ""MyConanProject/None"",
    ""options"": ""SomeLongOptionsString"",
    ""requires"": [
     ""1"",
     ""2"",
     ""3"",
     ""4"",
     ""5"",
     ""6""
    ],
    ""path"": ""../conanfile.py"",
    ""context"": ""host""
   },
   ""1"": {
    ""ref"": ""libabc/1.2.12#someHashOfLibAbc"",
    ""options"": ""someOptionsString"",
    ""package_id"": ""packageIdOfLibAbc"",
    ""prev"": ""someHashOfLibAbc"",
    ""context"": ""host""
   },
   ""2"": {
    ""ref"": ""libawesomelibrary/3.2.1#someHashOfLibAwesomeLibrary"",
    ""options"": ""someOptionsString"",
    ""package_id"": ""packageIdOfLibAwesomeLibrary"",
    ""prev"": ""someHashOfLibAwesomeLibrary"",
    ""requires"": [
        ""1""
    ],
    ""context"": ""host""
    },
   ""3"": {
    ""ref"": ""libanotherlibrary1/2.3.4#someHashOfLibAnotherLibrary1"",
    ""options"": ""someOptionsString"",
    ""package_id"": ""packageIdOfLibAnotherLibrary1"",
    ""prev"": ""someHashOfLibAnotherLibrary1"",
    ""requires"": [
     ""4"",
     ""6""
    ],
    ""context"": ""host""
    },
   ""4"": {
    ""ref"": ""libanotherlibrary2/3.4.5#someHashOfLibAnotherLibrary2"",
    ""options"": ""someOptionsString"",
    ""package_id"": ""packageIdOfLibAnotherLibrary2"",
    ""prev"": ""someHashOfLibAnotherLibrary2"",
    ""requires"": [
     ""5""
    ],
    ""context"": ""host""
    },
   ""5"": {
    ""ref"": ""libanotherlibrary3/4.5.6#someHashOfLibAnotherLibrary3"",
    ""options"": """",
    ""package_id"": ""packageIdOfLibAnotherLibrary3"",
    ""prev"": ""someHashOfLibAnotherLibrary3"",
    ""context"": ""host""
    },
   ""6"": {
    ""ref"": ""libanotherlibrary4/5.6.7#someHashOfLibAnotherLibrary4"",
    ""options"": ""someOptionsString"",
    ""package_id"": ""packageIdOfLibAnotherLibrary4"",
    ""prev"": ""someHashOfLibAnotherLibrary4"",
    ""modified"": true,
    ""context"": ""host""
    }
  },
  ""revisions_enabled"": true
 },
 ""version"": ""0.4"",
 ""profile_host"": ""someLongProfileHostSettingsString\n"",
 ""profile_build"": ""someLongProfileBuildSettingsString\n""
}
";

    private readonly string testConanLockStringWithNullValueForRootNode = @"{
 ""graph_lock"": {
  ""nodes"": {
   ""0"": null,
   ""1"": {
    ""ref"": ""libabc/1.2.12#someHashOfLibAbc"",
    ""options"": ""someOptionsString"",
    ""package_id"": ""packageIdOfLibAbc"",
    ""prev"": ""someHashOfLibAbc"",
    ""context"": ""host""
   }
  },
  ""revisions_enabled"": true
 },
 ""version"": ""0.4"",
 ""profile_host"": ""someLongProfileHostSettingsString\n"",
 ""profile_build"": ""someLongProfileBuildSettingsString\n""
}
";

    private readonly string testConanLockNoDependenciesString = @"{
 ""graph_lock"": {
  ""nodes"": {
   ""0"": {
    ""ref"": ""MyConanProject/None"",
    ""options"": ""SomeLongOptionsString"",
    ""path"": ""../conanfile.py"",
    ""context"": ""host""
   }
  },
  ""revisions_enabled"": true
 },
 ""version"": ""0.4"",
 ""profile_host"": ""someLongProfileHostSettingsString\n"",
 ""profile_build"": ""someLongProfileBuildSettingsString\n""
}
";

    [TestMethod]
    public async Task TestGraphIsCorrectAsync()
    {
        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Conan.lock", this.testConanLockString)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(6);

        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First(); // There should only be 1

        // Verify explicitly referenced roots
        var rootComponents = new List<string>
        {
            "libabc 1.2.12#someHashOfLibAbc - Conan",
            "libawesomelibrary 3.2.1#someHashOfLibAwesomeLibrary - Conan",
            "libanotherlibrary1 2.3.4#someHashOfLibAnotherLibrary1 - Conan",
            "libanotherlibrary2 3.4.5#someHashOfLibAnotherLibrary2 - Conan",
            "libanotherlibrary3 4.5.6#someHashOfLibAnotherLibrary3 - Conan",
            "libanotherlibrary4 5.6.7#someHashOfLibAnotherLibrary4 - Conan",
        };

        var enumerable = graph.GetComponents().ToList();

        rootComponents.ForEach(rootComponentId =>
        {
            graph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue($"Expected Component to be explicitly referenced but it is not: {rootComponentId}");
        });

        // components without any dependencies
        graph.GetDependenciesForComponent("libabc 1.2.12#someHashOfLibAbc - Conan").Should().BeEmpty();
        graph.GetDependenciesForComponent("libanotherlibrary3 4.5.6#someHashOfLibAnotherLibrary3 - Conan").Should().BeEmpty();
        graph.GetDependenciesForComponent("libanotherlibrary4 5.6.7#someHashOfLibAnotherLibrary4 - Conan").Should().BeEmpty();

        // Verify dependencies for other dependencies
        graph.GetDependenciesForComponent("libawesomelibrary 3.2.1#someHashOfLibAwesomeLibrary - Conan").Should().BeEquivalentTo(["libabc 1.2.12#someHashOfLibAbc - Conan"]);
        var a = graph.GetDependenciesForComponent("libanotherlibrary1 2.3.4#someHashOfLibAnotherLibrary1 - Conan");
        graph.GetDependenciesForComponent("libanotherlibrary1 2.3.4#someHashOfLibAnotherLibrary1 - Conan").Should().BeEquivalentTo(["libanotherlibrary2 3.4.5#someHashOfLibAnotherLibrary2 - Conan", "libanotherlibrary4 5.6.7#someHashOfLibAnotherLibrary4 - Conan"]);
        graph.GetDependenciesForComponent("libanotherlibrary2 3.4.5#someHashOfLibAnotherLibrary2 - Conan").Should().BeEquivalentTo(["libanotherlibrary3 4.5.6#someHashOfLibAnotherLibrary3 - Conan"]);
    }

    [TestMethod]
    public async Task TestDetectionForConanLockFileWithNullValuesForRootNodeAsync()
    {
        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Conan.lock", this.testConanLockStringWithNullValueForRootNode)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().ContainSingle();

        (componentRecorder.GetDetectedComponents().First().Component as ConanComponent).Name.Should().Be("libabc");
        (componentRecorder.GetDetectedComponents().First().Component as ConanComponent).Version.Should().Be("1.2.12#someHashOfLibAbc");
    }

    [TestMethod]
    public async Task TestConanDetectorAsync()
    {
        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Conan.lock", this.testConanLockString)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(6);

        IDictionary<string, string> packageVersions = new Dictionary<string, string>()
        {
            { "libabc", "1.2.12#someHashOfLibAbc" },
            { "libawesomelibrary", "3.2.1#someHashOfLibAwesomeLibrary" },
            { "libanotherlibrary1", "2.3.4#someHashOfLibAnotherLibrary1" },
            { "libanotherlibrary2", "3.4.5#someHashOfLibAnotherLibrary2" },
            { "libanotherlibrary3", "4.5.6#someHashOfLibAnotherLibrary3" },
            { "libanotherlibrary4", "5.6.7#someHashOfLibAnotherLibrary4" },
        };

        IDictionary<string, ISet<string>> packageDependencyRoots = new Dictionary<string, ISet<string>>()
        {
            { "libabc", new HashSet<string>() { "libabc", "libawesomelibrary" } },
            { "libawesomelibrary", new HashSet<string>() { "libawesomelibrary" } },
            { "libanotherlibrary1", new HashSet<string>() { "libanotherlibrary1" } },
            { "libanotherlibrary2", new HashSet<string>() { "libanotherlibrary2", "libanotherlibrary1" } },
            { "libanotherlibrary3", new HashSet<string>() { "libanotherlibrary3", "libanotherlibrary1", "libanotherlibrary2" } },
            { "libanotherlibrary4", new HashSet<string>() { "libanotherlibrary4", "libanotherlibrary1" } },
        };

        ISet<string> componentNames = new HashSet<string>();
        foreach (var discoveredComponent in componentRecorder.GetDetectedComponents())
        {
            // Verify each package has the right information
            var packageName = (discoveredComponent.Component as ConanComponent).Name;

            // Verify version
            (discoveredComponent.Component as ConanComponent).Version.Should().Be(packageVersions[packageName]);

            var dependencyRoots = new HashSet<string>();

            componentRecorder.AssertAllExplicitlyReferencedComponents(
                discoveredComponent.Component.Id,
                packageDependencyRoots[packageName].Select(expectedRoot =>
                    new Func<ConanComponent, bool>(parentComponent => parentComponent.Name == expectedRoot)).ToArray());

            componentNames.Add(packageName);
        }

        // Verify all packages were detected
        foreach (var expectedPackage in packageVersions.Keys)
        {
            componentNames.Should().Contain(expectedPackage);
        }
    }

    [TestMethod]
    public async Task TestConanDetector_SupportNoDependenciesAsync()
    {
        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("conan.lock", this.testConanLockNoDependenciesString)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().BeEmpty();
    }
}
