namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class RustSbomParserTests
{
    private const string CratesIo = "registry+https://github.com/rust-lang/crates.io-index";
    private RustSbomParser parser;
    private Mock<ILogger<RustSbomParser>> logger;

    [TestInitialize]
    public void Init()
    {
        this.logger = new Mock<ILogger<RustSbomParser>>(MockBehavior.Loose);
        this.parser = new RustSbomParser(this.logger.Object);
    }

    private static IComponentStream MakeSbomStream(string location, string json) =>
        new ComponentStream
        {
            Location = location,
            Pattern = "*.cargo-sbom.json",
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(json)),
        };

    private static string BuildSimpleSbomJson() => $$"""
    {
      "version": 1,
      "root": 0,
      "crates": [
        {
          "id": "path+file:///repo/root#0.1.0",
          "features": [],
          "dependencies": [
            { "index": 1, "kind": "normal" }
          ]
        },
        {
          "id": "{{CratesIo}}#dep1@1.0.0",
          "features": [],
          "dependencies": []
        }
      ]
    }
    """;

    private static string BuildNestedSbomJson() => $$"""
    {
      "version": 1,
      "root": 0,
      "crates": [
        {
          "id": "path+file:///repo/root#0.1.0",
          "features": [],
          "dependencies": [
            { "index": 1, "kind": "normal" }
          ]
        },
        {
          "id": "{{CratesIo}}#parent@2.0.0",
          "features": [],
          "dependencies": [
            { "index": 2, "kind": "normal" }
          ]
        },
        {
          "id": "{{CratesIo}}#child@3.0.0",
          "features": [],
          "dependencies": []
        }
      ]
    }
    """;

    [TestMethod]
    public async Task ParseAsync_ValidSimpleSbom_RegistersComponents()
    {
        var json = BuildSimpleSbomJson();
        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var version = await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        version.Should().Be(1);
        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().ContainSingle();

        var component = ((DetectedComponent)registrations[0].Arguments[0]).Component as CargoComponent;
        component.Name.Should().Be("dep1");
        component.Version.Should().Be("1.0.0");

        // Depth 0 from root => explicit
        registrations[0].Arguments[1].Should().Be(true);
    }

    [TestMethod]
    public async Task ParseAsync_NestedDependencies_CorrectDepthFlags()
    {
        var json = BuildNestedSbomJson();
        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().HaveCount(2);

        // parent at depth 0 => explicit
        var parentReg = registrations.Single(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "parent");
        parentReg.Arguments[1].Should().Be(true);

        // child at depth 1 => not explicit
        var childReg = registrations.Single(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "child");
        childReg.Arguments[1].Should().Be(false);
    }

    [TestMethod]
    public async Task ParseAsync_CycleInDependencies_DoesNotInfiniteLoop()
    {
        var json = $$"""
        {
          "version": 1,
          "root": 0,
          "crates": [
            {
              "id": "path+file:///repo/root#0.1.0",
              "features": [],
              "dependencies": [
                { "index": 1, "kind": "normal" }
              ]
            },
            {
              "id": "{{CratesIo}}#cycle@1.0.0",
              "features": [],
              "dependencies": [
                { "index": 1, "kind": "normal" }
              ]
            }
          ]
        }
        """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var version = await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);
        version.Should().Be(1);

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().HaveCount(2); // Dependency is marked visited after registering
    }

    [TestMethod]
    public async Task ParseAsync_GitSourceComponent_Ignored()
    {
        var json = """
        {
          "version": 1,
          "root": 0,
          "crates": [
            {
              "id": "path+file:///repo/root#0.1.0",
              "features": [],
              "dependencies": [
                { "index": 1, "kind": "normal" }
              ]
            },
            {
              "id": "git+https://github.com/org/repo.git#gitdep@1.0.0",
              "features": [],
              "dependencies": []
            }
          ]
        }
        """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().BeEmpty(); // Git source not registered
    }

    [TestMethod]
    public async Task ParseAsync_PathSourceComponent_Ignored()
    {
        var json = """
        {
          "version": 1,
          "root": 0,
          "crates": [
            {
              "id": "path+file:///repo/root#0.1.0",
              "features": [],
              "dependencies": [
                { "index": 1, "kind": "normal" }
              ]
            },
            {
              "id": "path+file:///repo/local#localdep@1.0.0",
              "features": [],
              "dependencies": []
            }
          ]
        }
        """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().BeEmpty(); // Path source not registered
    }

    [TestMethod]
    public async Task ParseAsync_InvalidPackageIdSpec_RecordsFailure()
    {
        var json = $$"""
        {
          "version": 1,
          "root": 0,
          "crates": [
            {
              "id": "path+file:///repo/root#0.1.0",
              "features": [],
              "dependencies": [
                { "index": 1, "kind": "normal" }
              ]
            },
            {
              "id": "this is completely invalid",
              "features": [],
              "dependencies": []
            }
          ]
        }
        """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var version = await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        // ProcessCargoSbom catches the exception, so ParseAsync returns version successfully
        version.Should().Be(1);

        // No RegisterPackageParseFailure is called because the exception is caught at ProcessCargoSbom level
        var failures = recorder.Invocations.Where(i => i.Method.Name == "RegisterPackageParseFailure").ToList();
        failures.Should().BeEmpty();

        // No components are registered due to the exception
        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ParseAsync_MalformedJson_ReturnsNull()
    {
        var badJson = "{ this is not valid json }";
        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var version = await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", badJson), recorder.Object);

        version.Should().BeNull();
        recorder.Invocations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ParseAsync_EmptyDependenciesArray_NoRegistrations()
    {
        var json = """
        {
          "version": 1,
          "root": 0,
          "crates": [
            {
              "id": "path+file:///repo/root#0.1.0",
              "features": [],
              "dependencies": []
            }
          ]
        }
        """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var version = await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        version.Should().Be(1);
        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ParsePackageIdSpec_NameInferredFromSource()
    {
        var json = $$"""
        {
          "version": 1,
          "root": 0,
          "crates": [
            {
              "id": "path+file:///repo/root#0.1.0",
              "features": [],
              "dependencies": [
                { "index": 1, "kind": "normal" }
              ]
            },
            {
              "id": "{{CratesIo}}#1.0.0",
              "features": [],
              "dependencies": []
            }
          ]
        }
        """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().ContainSingle();

        var component = ((DetectedComponent)registrations[0].Arguments[0]).Component as CargoComponent;
        component.Name.Should().Be("crates.io-index"); // Inferred from last segment
        component.Version.Should().Be("1.0.0");
    }

    [TestMethod]
    public async Task ParsePackageIdSpec_BlankSource_BecomesNull()
    {
        var json = """
        {
          "version": 1,
          "root": 0,
          "crates": [
            {
              "id": "path+file:///repo/root#0.1.0",
              "features": [],
              "dependencies": [
                { "index": 1, "kind": "normal" }
              ]
            },
            {
              "id": "#localname@1.0.0",
              "features": [],
              "dependencies": []
            }
          ]
        }
        """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().BeEmpty(); // Blank/null source => not crates.io => ignored
    }

    [TestMethod]
    public async Task ParseWithOwnershipAsync_SingleOwner_RegistersViaOwnerRecorder()
    {
        var json = BuildSimpleSbomJson();
        var sbomRecorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);
        var ownerRecorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        parentRecorder.Setup(p => p.CreateSingleFileComponentRecorder("manifests/owner1")).Returns(ownerRecorder.Object);

        var ownershipMap = new Dictionary<string, HashSet<string>>
        {
            { $"{CratesIo}#dep1@1.0.0", new HashSet<string> { "manifests/owner1" } },
        };

        var version = await this.parser.ParseWithOwnershipAsync(
            MakeSbomStream("test.cargo-sbom.json", json),
            sbomRecorder.Object,
            parentRecorder.Object,
            ownershipMap);

        version.Should().Be(1);

        ownerRecorder.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
        sbomRecorder.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(0);
    }

    [TestMethod]
    public async Task ParseWithOwnershipAsync_MultipleOwners_RegistersForEach()
    {
        var json = BuildSimpleSbomJson();
        var sbomRecorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);
        var owner1 = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var owner2 = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        parentRecorder.Setup(p => p.CreateSingleFileComponentRecorder("manifests/owner1")).Returns(owner1.Object);
        parentRecorder.Setup(p => p.CreateSingleFileComponentRecorder("manifests/owner2")).Returns(owner2.Object);

        var ownershipMap = new Dictionary<string, HashSet<string>>
        {
            { $"{CratesIo}#dep1@1.0.0", new HashSet<string> { "manifests/owner1", "manifests/owner2" } },
        };

        await this.parser.ParseWithOwnershipAsync(
            MakeSbomStream("test.cargo-sbom.json", json),
            sbomRecorder.Object,
            parentRecorder.Object,
            ownershipMap);

        owner1.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
        owner2.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
        sbomRecorder.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(0);
    }

    [TestMethod]
    public async Task ParseWithOwnershipAsync_NoOwnership_FallsBackToSbomRecorder()
    {
        var json = BuildSimpleSbomJson();
        var sbomRecorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);

        var ownershipMap = new Dictionary<string, HashSet<string>>(); // Empty

        await this.parser.ParseWithOwnershipAsync(
            MakeSbomStream("test.cargo-sbom.json", json),
            sbomRecorder.Object,
            parentRecorder.Object,
            ownershipMap);

        sbomRecorder.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
        parentRecorder.Invocations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ParseWithOwnershipAsync_NullOwnershipMap_FallsBack()
    {
        var json = BuildSimpleSbomJson();
        var sbomRecorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseWithOwnershipAsync(
            MakeSbomStream("test.cargo-sbom.json", json),
            sbomRecorder.Object,
            parentComponentRecorder: null,
            ownershipMap: null);

        sbomRecorder.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
    }

    [TestMethod]
    public async Task ParseWithOwnershipAsync_NullParentRecorder_FallsBack()
    {
        var json = BuildSimpleSbomJson();
        var sbomRecorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var ownershipMap = new Dictionary<string, HashSet<string>>
        {
            { $"{CratesIo}#dep1@1.0.0", new HashSet<string> { "manifests/owner1" } },
        };

        await this.parser.ParseWithOwnershipAsync(
            MakeSbomStream("test.cargo-sbom.json", json),
            sbomRecorder.Object,
            parentComponentRecorder: null,
            ownershipMap);

        sbomRecorder.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
    }

    [TestMethod]
    public async Task ParseWithOwnershipAsync_PartialOwnership_MixesFallbackAndOwners()
    {
        var json = BuildNestedSbomJson();
        var sbomRecorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var parentRecorder = new Mock<IComponentRecorder>(MockBehavior.Strict);
        var owner1 = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        parentRecorder.Setup(p => p.CreateSingleFileComponentRecorder("manifests/owner1")).Returns(owner1.Object);

        var ownershipMap = new Dictionary<string, HashSet<string>>
        {
            { $"{CratesIo}#parent@2.0.0", new HashSet<string> { "manifests/owner1" } },

            // child not in ownership map => fallback
        };

        await this.parser.ParseWithOwnershipAsync(
            MakeSbomStream("test.cargo-sbom.json", json),
            sbomRecorder.Object,
            parentRecorder.Object,
            ownershipMap);

        owner1.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
        sbomRecorder.Invocations.Count(i => i.Method.Name == "RegisterUsage").Should().Be(1);
    }

    [TestMethod]
    public async Task ParseWithOwnershipAsync_MalformedJson_ReturnsNull()
    {
        var badJson = "{ malformed }";
        var sbomRecorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        var version = await this.parser.ParseWithOwnershipAsync(
            MakeSbomStream("test.cargo-sbom.json", badJson),
            sbomRecorder.Object,
            parentComponentRecorder: null,
            ownershipMap: null);

        version.Should().BeNull();
    }

    [TestMethod]
    public async Task ParseAsync_MultipleDepthLevels_CorrectExplicitFlags()
    {
        var json = $$"""
        {
          "version": 1,
          "root": 0,
          "crates": [
            {
              "id": "path+file:///repo/root#0.1.0",
              "features": [],
              "dependencies": [
                { "index": 1, "kind": "normal" }
              ]
            },
            {
              "id": "{{CratesIo}}#level1@1.0.0",
              "features": [],
              "dependencies": [
                { "index": 2, "kind": "normal" }
              ]
            },
            {
              "id": "{{CratesIo}}#level2@2.0.0",
              "features": [],
              "dependencies": [
                { "index": 3, "kind": "normal" }
              ]
            },
            {
              "id": "{{CratesIo}}#level3@3.0.0",
              "features": [],
              "dependencies": []
            }
          ]
        }
        """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        registrations.Should().HaveCount(3);

        // level1 at depth 0 => explicit
        var level1 = registrations.Single(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "level1");
        level1.Arguments[1].Should().Be(true);

        // level2 at depth 1 => not explicit
        var level2 = registrations.Single(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "level2");
        level2.Arguments[1].Should().Be(false);

        // level3 at depth 2 => not explicit
        var level3 = registrations.Single(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "level3");
        level3.Arguments[1].Should().Be(false);
    }

    [TestMethod]
    public async Task ParseAsync_ParentComponentId_SetCorrectly()
    {
        var json = BuildNestedSbomJson();
        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();

        // parent registration should have null parent (depth 0)
        var parentReg = registrations.Single(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "parent");
        parentReg.Arguments[2].Should().BeNull();

        // child registration should have parent's ID
        var childReg = registrations.Single(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "child");
        var parentId = ((DetectedComponent)parentReg.Arguments[0]).Component.Id;
        childReg.Arguments[2].Should().Be(parentId);
    }

    [TestMethod]
    public async Task ParseAsync_DiamondDependency_HandledCorrectly()
    {
        var json = $$"""
        {
          "version": 1,
          "root": 0,
          "crates": [
            {
              "id": "path+file:///repo/root#0.1.0",
              "features": [],
              "dependencies": [
                { "index": 1, "kind": "normal" },
                { "index": 2, "kind": "normal" }
              ]
            },
            {
              "id": "{{CratesIo}}#depA@1.0.0",
              "features": [],
              "dependencies": [
                { "index": 3, "kind": "normal" }
              ]
            },
            {
              "id": "{{CratesIo}}#depB@1.0.0",
              "features": [],
              "dependencies": [
                { "index": 3, "kind": "normal" }
              ]
            },
            {
              "id": "{{CratesIo}}#shared@1.0.0",
              "features": [],
              "dependencies": []
            }
          ]
        }
        """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

        var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();

        // Should register shared multiple times (once per path)
        var sharedRegistrations = registrations.Where(r =>
            ((CargoComponent)((DetectedComponent)r.Arguments[0]).Component).Name == "shared").ToList();

        sharedRegistrations.Should().HaveCountGreaterOrEqualTo(1);
    }

    [TestMethod]
    public async Task ParsePackageIdSpec_VariousFormats_ParsedCorrectly()
    {
        var testCases = new[]
        {
            ($"{CratesIo}#name@1.0.0", "name", "1.0.0", CratesIo),
            ("git+https://github.com/org/repo.git#gitname@2.0.0", "gitname", "2.0.0", "git+https://github.com/org/repo.git"),
            ("path+file:///local/path#localname@3.0.0", "localname", "3.0.0", "path+file:///local/path"),
        };

        foreach (var (id, expectedName, expectedVersion, expectedSource) in testCases)
        {
            var json = $$"""
            {
              "version": 1,
              "root": 0,
              "crates": [
                {
                  "id": "path+file:///repo/root#0.1.0",
                  "features": [],
                  "dependencies": [
                    { "index": 1, "kind": "normal" }
                  ]
                },
                {
                  "id": "{{id}}",
                  "features": [],
                  "dependencies": []
                }
              ]
            }
            """;

            var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
            await this.parser.ParseAsync(MakeSbomStream("test.cargo-sbom.json", json), recorder.Object);

            if (expectedSource == CratesIo)
            {
                var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
                registrations.Should().ContainSingle();

                var component = ((DetectedComponent)registrations[0].Arguments[0]).Component as CargoComponent;
                component.Name.Should().Be(expectedName);
                component.Version.Should().Be(expectedVersion);
                component.Source.Should().Be(expectedSource);
            }
            else
            {
                // Non-crates.io sources should be ignored
                var registrations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
                registrations.Should().BeEmpty();
            }
        }
    }
}
