namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class RustCargoLockParserTests
{
    private const string CratesIo = "registry+https://github.com/rust-lang/crates.io-index";
    private RustCargoLockParser parser;
    private Mock<ILogger<RustCargoLockParser>> logger;

    [TestInitialize]
    public void Init()
    {
        this.logger = new Mock<ILogger<RustCargoLockParser>>();
        this.parser = new RustCargoLockParser(this.logger.Object);
    }

    private static IComponentStream MakeStream(string name, string toml)
    {
        return new ComponentStream
        {
            Location = name,
            Pattern = "Cargo.lock",
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(toml)),
        };
    }

    private static (int Usages, int ExplicitRoots, int Edges, int Failures) Analyze(Mock<ISingleFileComponentRecorder> recorder)
    {
        var usageInvocations = recorder.Invocations.Where(i => i.Method.Name == "RegisterUsage").ToList();
        var explicitRoots = 0;
        var edges = 0;
        foreach (var inv in usageInvocations)
        {
            // Signature: RegisterUsage(DetectedComponent dc, bool isExplicitReferencedDependency = false, string parentComponentId = null, bool isDevelopmentDependency = false)
            if (inv.Arguments.Count >= 2 && inv.Arguments[1] is bool explicitFlag && explicitFlag)
            {
                explicitRoots++;
            }

            if (inv.Arguments.Count >= 3 && inv.Arguments[2] is string parentId)
            {
                edges++;
            }
        }

        var failures = recorder.Invocations.Count(i => i.Method.Name == "RegisterPackageParseFailure");
        return (usageInvocations.Count, explicitRoots, edges, failures);
    }

    [TestMethod]
    public async Task ParseAsync_NoPackages_ReturnsVersion_NoUsage()
    {
        var toml = """
                   version = 3
                   # No [[package]] tables
                   """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var version = await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object, CancellationToken.None);

        version.Should().Be(3);
        var (usages, explicitRoots, edges, failures) = Analyze(recorder);
        usages.Should().Be(0);
        explicitRoots.Should().Be(0);
        edges.Should().Be(0);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task SingleRemotePackage_RootMarked()
    {
        var toml = $"""
                    version = 3

                    [[package]]
                    name = "foo"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // Pass1 initial + Pass3 explicit root
        usages.Should().Be(2);
        explicitRoots.Should().Be(1);
        edges.Should().Be(0);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task LocalOnlyPackage_NoUsage()
    {
        var toml = """
                   [[package]]
                   name = "local"
                   version = "0.1.0"
                   # no source => local
                   """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);
        usages.Should().Be(0);
        explicitRoots.Should().Be(0);
        edges.Should().Be(0);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task RemoteParentDependsOnRemoteChild_EdgeAndRoot()
    {
        var toml = $"""
                    [[package]]
                    name = "parent"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    dependencies = [
                        "child 2.0.0 ({CratesIo})"
                    ]

                    [[package]]
                    name = "child"
                    version = "2.0.0"
                    source = "{CratesIo}"
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // parent + child initial =2, child edge registration =1, plus root marking for parent =1 => total 4
        usages.Should().Be(4);
        explicitRoots.Should().Be(1);   // only parent (child seen as dependency)
        edges.Should().Be(1);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task LocalParentDependsOnRemoteChild_ChildMarkedExplicitThroughEdge()
    {
        var toml = $"""
                    [[package]]
                    name = "local_parent"
                    version = "0.1.0"

                    [[package]]
                    name = "child"
                    version = "1.2.3"
                    source = "{CratesIo}"

                    [[package]]
                    name = "local_parent"
                    version = "0.1.0"
                    # duplicate logically (different table not needed but dependencies on another instance is ok)
                    dependencies = [
                        "child 1.2.3 ({CratesIo})"
                    ]
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // child initial + explicit (from local parent edge) =2 usages, no Pass3 root (child is dependency)
        usages.Should().BeGreaterThanOrEqualTo(2);
        explicitRoots.Should().Be(1); // the explicit edge registration
        edges.Should().Be(0); // edge from local parent recorded as root usage (no parentComponentId)
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task DependencyOnLocalChild_Ignored()
    {
        var toml = $"""
                    [[package]]
                    name = "parent"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    dependencies = [
                        "local 0.1.0"
                    ]

                    [[package]]
                    name = "local"
                    version = "0.1.0"
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // parent initial + root marking
        usages.Should().Be(2);
        explicitRoots.Should().Be(1);
        edges.Should().Be(0); // local child edge skipped
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task AmbiguousDependency_ParseFailure()
    {
        var toml = $"""
                    [[package]]
                    name = "parent"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    dependencies = [
                        "dup"
                    ]

                    [[package]]
                    name = "dup"
                    version = "1.0.0"
                    source = "{CratesIo}"

                    [[package]]
                    name = "dup"
                    version = "2.0.0"
                    source = "{CratesIo}"
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // Initial registrations: parent + two dup variants = 3
        // Root markings (all three remote, none seen as dependency due to failure) => +3 = 6 total
        usages.Should().Be(6);
        explicitRoots.Should().Be(3);
        edges.Should().Be(0);
        failures.Should().Be(1); // one failure for ambiguous dep
    }

    [TestMethod]
    public async Task DependencyVersionMismatch_ParseFailure()
    {
        var toml = $"""
                    [[package]]
                    name = "parent"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    dependencies = [
                        "child 2.0.0"
                    ]

                    [[package]]
                    name = "child"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (_, explicitRoots, edges, failures) = Analyze(recorder);
        explicitRoots.Should().Be(2); // parent + child (child not seen as dependency because mismatch)
        edges.Should().Be(0);
        failures.Should().Be(1);
    }

    [TestMethod]
    public async Task DependencySourceMismatch_ParseFailure()
    {
        var toml = $"""
                    [[package]]
                    name = "parent"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    dependencies = [
                        "child 1.0.0 (some+other+registry)"
                    ]

                    [[package]]
                    name = "child"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (_, explicitRoots, edges, failures) = Analyze(recorder);
        explicitRoots.Should().Be(2); // parent + child
        edges.Should().Be(0);
        failures.Should().Be(1);
    }

    [TestMethod]
    public async Task MalformedDependencyString_ParseFailure()
    {
        var toml = $"""
                    [[package]]
                    name = "parent"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    dependencies = [
                        "child 1.0.0 ({CratesIo}",    # missing closing paren => no match
                        "   "                         # whitespace
                    ]

                    [[package]]
                    name = "child"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (_, explicitRoots, edges, failures) = Analyze(recorder);
        explicitRoots.Should().Be(2); // parent + child (child not seen)
        edges.Should().Be(0);
        failures.Should().Be(2);
    }

    [TestMethod]
    public async Task DependencyRegexVariants_AllResolved()
    {
        // Valid dependency forms (per Cargo.lock expectations):
        // name
        // name version
        // name version (source)
        // (name + source without version is intentionally NOT present)
        var toml = $"""
                    [[package]]
                    name = "parent"
                    version = "0.1.0"
                    source = "{CratesIo}"
                    dependencies = [
                        "onlyname",
                        "withver 1.0.0",
                        "withboth 2.0.0 ({CratesIo})"
                    ]

                    [[package]]
                    name = "onlyname"
                    version = "9.9.9"
                    source = "{CratesIo}"

                    [[package]]
                    name = "withver"
                    version = "1.0.0"
                    source = "{CratesIo}"

                    [[package]]
                    name = "withboth"
                    version = "2.0.0"
                    source = "{CratesIo}"
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (_, explicitRoots, edges, failures) = Analyze(recorder);
        failures.Should().Be(0);

        // parent is the only explicit root; all children referenced as dependencies
        explicitRoots.Should().Be(1);
        edges.Should().Be(3);
    }

    [TestMethod]
    public async Task DuplicatePackageEntries_SecondIgnored()
    {
        var toml = $"""
                    [[package]]
                    name = "dup"
                    version = "1.0.0"
                    source = "{CratesIo}"

                    [[package]]
                    name = "dup"
                    version = "1.0.0"
                    source = "{CratesIo}"
                    """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // First registration + root marking = 2
        usages.Should().Be(2);
        explicitRoots.Should().Be(1);
        edges.Should().Be(0);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task ParseAsync_TomlParseFailure_ReturnsNull()
    {
        var badToml = """
                      [[package
                      name = "broken"
                      """; // malformed

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        var version = await this.parser.ParseAsync(MakeStream("Cargo.lock", badToml), recorder.Object);
        version.Should().BeNull();

        // No usage attempts on parse failure
        Analyze(recorder).Usages.Should().Be(0);
    }

    [TestMethod]
    public async Task MultipleRemotePackages_WithoutDependencies_AllMarkedAsRoots()
    {
        // Tests that multiple independent remote packages are all marked as explicit roots
        var toml = $"""
                version = 3

                [[package]]
                name = "package-a"
                version = "1.0.0"
                source = "{CratesIo}"

                [[package]]
                name = "package-b"
                version = "2.0.0"
                source = "{CratesIo}"

                [[package]]
                name = "package-c"
                version = "3.0.0"
                source = "{CratesIo}"
                """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // Each package: initial registration + root marking = 2 * 3 = 6
        usages.Should().Be(6);
        explicitRoots.Should().Be(3);
        edges.Should().Be(0);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task ComplexDependencyGraph_MultipleParentsOneChild()
    {
        // Tests that a child with multiple parents is handled correctly
        var toml = $"""
                [[package]]
                name = "parent-a"
                version = "1.0.0"
                source = "{CratesIo}"
                dependencies = [
                    "shared 1.0.0 ({CratesIo})"
                ]

                [[package]]
                name = "parent-b"
                version = "2.0.0"
                source = "{CratesIo}"
                dependencies = [
                    "shared 1.0.0 ({CratesIo})"
                ]

                [[package]]
                name = "shared"
                version = "1.0.0"
                source = "{CratesIo}"
                """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // Initial: parent-a, parent-b, shared = 3
        // Edges: 2 (one from each parent)
        // Roots: parent-a, parent-b = 2 (shared is seen as dependency)
        usages.Should().Be(7); // 3 initial + 2 edges + 2 roots
        explicitRoots.Should().Be(2);
        edges.Should().Be(2);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task TransitiveDependencies_ThreeLevels()
    {
        // Tests a chain: root -> intermediate -> leaf
        var toml = $"""
                [[package]]
                name = "root"
                version = "1.0.0"
                source = "{CratesIo}"
                dependencies = [
                    "intermediate 1.0.0 ({CratesIo})"
                ]

                [[package]]
                name = "intermediate"
                version = "1.0.0"
                source = "{CratesIo}"
                dependencies = [
                    "leaf 1.0.0 ({CratesIo})"
                ]

                [[package]]
                name = "leaf"
                version = "1.0.0"
                source = "{CratesIo}"
                """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // Initial: 3 packages
        // Edges: 2 (root->intermediate, intermediate->leaf)
        // Roots: 1 (only root)
        usages.Should().Be(6); // 3 initial + 2 edges + 1 root
        explicitRoots.Should().Be(1);
        edges.Should().Be(2);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task MixedLocalAndRemote_ComplexGraph()
    {
        // Tests a mix of local and remote packages in a more complex graph
        var toml = $"""
                [[package]]
                name = "local-root"
                version = "0.1.0"
                dependencies = [
                    "remote-dep 1.0.0 ({CratesIo})"
                ]

                [[package]]
                name = "remote-dep"
                version = "1.0.0"
                source = "{CratesIo}"
                dependencies = [
                    "local-intermediate 0.2.0"
                ]

                [[package]]
                name = "local-intermediate"
                version = "0.2.0"
                dependencies = [
                    "remote-leaf 2.0.0 ({CratesIo})"
                ]

                [[package]]
                name = "remote-leaf"
                version = "2.0.0"
                source = "{CratesIo}"
                """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // remote-dep: initial + explicit (from local-root) = 2
        // remote-leaf: initial (local-intermediate ignored as parent) = 1
        // remote-leaf should be marked as root since local parent edge doesn't count
        usages.Should().BeGreaterThanOrEqualTo(3);
        explicitRoots.Should().BeGreaterThanOrEqualTo(1);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task DependencyWithEmptyString_ParseFailure()
    {
        // Tests handling of empty dependency strings
        var toml = $"""
                [[package]]
                name = "parent"
                version = "1.0.0"
                source = "{CratesIo}"
                dependencies = [
                    ""
                ]

                [[package]]
                name = "child"
                version = "1.0.0"
                source = "{CratesIo}"
                """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (_, explicitRoots, edges, failures) = Analyze(recorder);

        explicitRoots.Should().Be(2); // Both marked as roots (no valid edge created)
        edges.Should().Be(0);
        failures.Should().Be(1);
    }

    [TestMethod]
    public async Task NonExistentDependency_ParseFailure()
    {
        // Tests when a dependency references a package that doesn't exist in the lock file
        var toml = $"""
                [[package]]
                name = "parent"
                version = "1.0.0"
                source = "{CratesIo}"
                dependencies = [
                    "nonexistent 1.0.0 ({CratesIo})"
                ]
                """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (_, explicitRoots, edges, failures) = Analyze(recorder);

        explicitRoots.Should().Be(1); // Only parent
        edges.Should().Be(0);
        failures.Should().Be(1);
    }

    [TestMethod]
    public async Task ProcessCargoLock_ExceptionHandling_ContinuesGracefully()
    {
        // Tests that exceptions in ProcessCargoLock are caught and logged
        // This would require a specially crafted TOML that parses but causes issues in processing
        var toml = $"""
                version = 3
                
                [[package]]
                name = "valid"
                version = "1.0.0"
                source = "{CratesIo}"
                """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);

        // Should not throw even if processing has issues
        var act = async () => await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);
        await act.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task NullOrEmptyDependenciesArray_NoFailures()
    {
        // Tests package with explicit empty dependencies array
        var toml = $"""
                [[package]]
                name = "package"
                version = "1.0.0"
                source = "{CratesIo}"
                dependencies = []
                """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        usages.Should().Be(2); // Initial + root marking
        explicitRoots.Should().Be(1);
        edges.Should().Be(0);
        failures.Should().Be(0);
    }

    [TestMethod]
    public async Task DifferentVersionsSamePackage_BothRegistered()
    {
        // Tests that different versions of the same package are treated separately
        var toml = $"""
                [[package]]
                name = "parent"
                version = "1.0.0"
                source = "{CratesIo}"
                dependencies = [
                    "dep 1.0.0 ({CratesIo})",
                    "dep 2.0.0 ({CratesIo})"
                ]

                [[package]]
                name = "dep"
                version = "1.0.0"
                source = "{CratesIo}"

                [[package]]
                name = "dep"
                version = "2.0.0"
                source = "{CratesIo}"
                """;

        var recorder = new Mock<ISingleFileComponentRecorder>(MockBehavior.Loose);
        await this.parser.ParseAsync(MakeStream("Cargo.lock", toml), recorder.Object);

        var (usages, explicitRoots, edges, failures) = Analyze(recorder);

        // parent + dep v1 + dep v2 initial = 3
        // 2 edges (parent -> dep v1, parent -> dep v2) = 2
        // 1 root (parent) = 1
        usages.Should().Be(6);
        explicitRoots.Should().Be(1);
        edges.Should().Be(2);
        failures.Should().Be(0);
    }
}
