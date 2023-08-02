namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Conan;
using Microsoft.ComponentDetection.Detectors.Conan.Contracts;
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
    public void TestConanLock_SerializeDeserialize()
    {
        var sampleConanLock = new ConanLock
        {
            Version = "0.4",
            ProfileBuild = "someProfileBuild",
            GraphLock = new ConanLockGraph
            {
                RevisionsEnabled = true,
                Nodes = new Dictionary<string, ConanLockNode>()
                {
                    {
                        "0", new ConanLockNode
                        {
                            Context = string.Empty,
                            Modified = false,
                            Options = string.Empty,
                            PackageId = "SomePackageId",
                            Path = "D:/SomePath",
                            Previous = "SomeOtherId",
                            Reference = string.Empty,
                            Requires = new[] { "0" },
                        }
                    },
                },
            },
        };
        var jsonString = JsonSerializer.Serialize(sampleConanLock, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault });
        var expectedJsonString = @"{""version"":""0.4"",""profile_build"":""someProfileBuild"",""graph_lock"":{""revisions_enabled"":true,""nodes"":{""0"":{""context"":"""",""modified"":false,""options"":"""",""package_id"":""SomePackageId"",""path"":""D:/SomePath"",""prev"":""SomeOtherId"",""ref"":"""",""requires"":[""0""]}}}}";

        Assert.AreEqual(expectedJsonString, jsonString);

        var deserialisedConanLock = JsonSerializer.Deserialize<ConanLock>(expectedJsonString);
        Assert.AreEqual(sampleConanLock, deserialisedConanLock);
    }

    [TestMethod]
    public async Task TestGraphIsCorrectAsync()
    {
        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Conan.lock", this.testConanLockString)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
        Assert.AreEqual(7, componentRecorder.GetDetectedComponents().Count());

        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First(); // There should only be 1

        // Verify explicitly referenced roots
        var rootComponents = new List<string>
        {
            "MyConanProject None - Conan",
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
            try
            {
                graph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue();
            }
            catch
            {
                Assert.Fail($"Expected Component to be explicitly referenced but it is not: {rootComponentId}");
            }
        });

        // components without any dependencies
        graph.GetDependenciesForComponent("libabc 1.2.12#someHashOfLibAbc - Conan").Should().BeEmpty();
        graph.GetDependenciesForComponent("libanotherlibrary3 4.5.6#someHashOfLibAnotherLibrary3 - Conan").Should().BeEmpty();
        graph.GetDependenciesForComponent("libanotherlibrary4 5.6.7#someHashOfLibAnotherLibrary4 - Conan").Should().BeEmpty();

        // Verify dependencies for other dependencies
        graph.GetDependenciesForComponent("libawesomelibrary 3.2.1#someHashOfLibAwesomeLibrary - Conan").Should().BeEquivalentTo(new[] { "libabc 1.2.12#someHashOfLibAbc - Conan" });
        var a = graph.GetDependenciesForComponent("libanotherlibrary1 2.3.4#someHashOfLibAnotherLibrary1 - Conan");
        graph.GetDependenciesForComponent("libanotherlibrary1 2.3.4#someHashOfLibAnotherLibrary1 - Conan").Should().BeEquivalentTo(new[] { "libanotherlibrary2 3.4.5#someHashOfLibAnotherLibrary2 - Conan", "libanotherlibrary4 5.6.7#someHashOfLibAnotherLibrary4 - Conan" });
        graph.GetDependenciesForComponent("libanotherlibrary2 3.4.5#someHashOfLibAnotherLibrary2 - Conan").Should().BeEquivalentTo(new[] { "libanotherlibrary3 4.5.6#someHashOfLibAnotherLibrary3 - Conan" });
    }

    [TestMethod]
    public async Task TestConanDetectorAsync()
    {
        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("Conan.lock", this.testConanLockString)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
        Assert.AreEqual(7, componentRecorder.GetDetectedComponents().Count());

        IDictionary<string, string> packageVersions = new Dictionary<string, string>()
        {
            { "MyConanProject", "None" },
            { "libabc", "1.2.12#someHashOfLibAbc" },
            { "libawesomelibrary", "3.2.1#someHashOfLibAwesomeLibrary" },
            { "libanotherlibrary1", "2.3.4#someHashOfLibAnotherLibrary1" },
            { "libanotherlibrary2", "3.4.5#someHashOfLibAnotherLibrary2" },
            { "libanotherlibrary3", "4.5.6#someHashOfLibAnotherLibrary3" },
            { "libanotherlibrary4", "5.6.7#someHashOfLibAnotherLibrary4" },
        };

        IDictionary<string, ISet<string>> packageDependencyRoots = new Dictionary<string, ISet<string>>()
        {
            { "MyConanProject", new HashSet<string>() { "MyConanProject" } },
            { "libabc", new HashSet<string>() { "libabc", "MyConanProject", "libawesomelibrary" } },
            { "libawesomelibrary", new HashSet<string>() { "libawesomelibrary", "MyConanProject" } },
            { "libanotherlibrary1", new HashSet<string>() { "libanotherlibrary1", "MyConanProject" } },
            { "libanotherlibrary2", new HashSet<string>() { "libanotherlibrary2", "MyConanProject", "libanotherlibrary1" } },
            { "libanotherlibrary3", new HashSet<string>() { "libanotherlibrary3", "MyConanProject", "libanotherlibrary1", "libanotherlibrary2" } },
            { "libanotherlibrary4", new HashSet<string>() { "libanotherlibrary4", "MyConanProject", "libanotherlibrary1" } },
        };

        ISet<string> componentNames = new HashSet<string>();
        foreach (var discoveredComponent in componentRecorder.GetDetectedComponents())
        {
            // Verify each package has the right information
            var packageName = (discoveredComponent.Component as ConanComponent).Name;

            // Verify version
            Assert.AreEqual(packageVersions[packageName], (discoveredComponent.Component as ConanComponent).Version);

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
            Assert.IsTrue(componentNames.Contains(expectedPackage));
        }
    }

    [TestMethod]
    public async Task TestConanDetector_SupportNoDependenciesAsync()
    {
        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("conan.lock", this.testConanLockNoDependenciesString)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, result.ResultCode);
        Assert.AreEqual(1, componentRecorder.GetDetectedComponents().Count());
    }
}
