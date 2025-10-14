#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PipDependencySpecifierTests
{
    private static void VerifyPipDependencyParsing(
        List<(string SpecString, PipDependencySpecification ReferenceDependencySpecification)> testCases,
        bool requiresDist = false)
    {
        foreach (var (specString, referenceDependencySpecification) in testCases)
        {
            var dependencySpecifier = new PipDependencySpecification(specString, requiresDist);

            dependencySpecifier.Name.Should().Be(referenceDependencySpecification.Name);
            dependencySpecifier.DependencySpecifiers.Should().HaveCount(referenceDependencySpecification.DependencySpecifiers.Count);
            for (var i = 0; i < referenceDependencySpecification.DependencySpecifiers.Count; i++)
            {
                dependencySpecifier.DependencySpecifiers.Should().HaveElementAt(
                    i, referenceDependencySpecification.DependencySpecifiers[i]);
            }

            dependencySpecifier.ConditionalDependencySpecifiers.Should().HaveCount(referenceDependencySpecification.ConditionalDependencySpecifiers.Count);
            for (var i = 0; i < referenceDependencySpecification.ConditionalDependencySpecifiers.Count; i++)
            {
                dependencySpecifier.ConditionalDependencySpecifiers.Should().HaveElementAt(
                    i, referenceDependencySpecification.ConditionalDependencySpecifiers[i]);
            }
        }
    }

    private static void VerifyPipConditionalDependencyParsing(
        List<(string SpecString, bool ShouldBeIncluded, PipDependencySpecification ReferenceDependencySpecification)> testCases,
        Dictionary<string, string> pythonEnvironmentVariables,
        bool requiresDist = false)
    {
        foreach (var (specString, shouldBeIncluded, referenceDependencySpecification) in testCases)
        {
            var dependencySpecifier = new PipDependencySpecification(specString, requiresDist);

            dependencySpecifier.Name.Should().Be(referenceDependencySpecification.Name);
            dependencySpecifier.DependencySpecifiers.Should().HaveCount(referenceDependencySpecification.DependencySpecifiers.Count);
            for (var i = 0; i < referenceDependencySpecification.DependencySpecifiers.Count; i++)
            {
                dependencySpecifier.DependencySpecifiers.Should().HaveElementAt(
                    i, referenceDependencySpecification.DependencySpecifiers[i]);
            }

            dependencySpecifier.ConditionalDependencySpecifiers.Should().HaveCount(referenceDependencySpecification.ConditionalDependencySpecifiers.Count);
            for (var i = 0; i < referenceDependencySpecification.ConditionalDependencySpecifiers.Count; i++)
            {
                dependencySpecifier.ConditionalDependencySpecifiers.Should().HaveElementAt(
                    i, referenceDependencySpecification.ConditionalDependencySpecifiers[i]);
            }

            dependencySpecifier.PackageConditionsMet(pythonEnvironmentVariables).Should().Be(shouldBeIncluded, string.Join(',', dependencySpecifier.ConditionalDependencySpecifiers));
        }
    }

    [TestMethod]
    public void TestPipDependencySpecifierConstruction()
    {
        var specs = new List<(string, PipDependencySpecification)>
        {
            ("TestPackage==1.0", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = ["==1.0"] }),
            ("TestPackage>=1.0,!=1.1", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=1.0", "!=1.1"] }),
            ("OtherPackage!=1.2,>=1.0,<=1.9,~=1.4", new PipDependencySpecification { Name = "OtherPackage", DependencySpecifiers = ["!=1.2", ">=1.0", "<=1.9", "~=1.4"] }),
            ("TestPackage[Optional]<3,>=1.0.0", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = ["<3", ">=1.0.0"] }),
        };

        VerifyPipDependencyParsing(specs);
    }

    [TestMethod]
    public void TestPipDependencyRequireDist()
    {
        var specs = new List<(string, PipDependencySpecification)>
        {
            ("Requires-Dist: TestPackage<1.27.0,>=1.19.5", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = ["<1.27.0", ">=1.19.5"] }),
            ("Requires-Dist: TestPackage (>=1.0.0) ; sys_platform == \"win32\"", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=1.0.0"], ConditionalDependencySpecifiers = ["sys_platform == \"win32\""] }),
            ("Requires-Dist: OtherPackage[Optional] (<3,>=1.0.0)", new PipDependencySpecification { Name = "OtherPackage", DependencySpecifiers = ["<3", ">=1.0.0"] }),
            ("Requires-Dist: TestPackage (>=3.7.4.3) ; python_version < \"3.8\"", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=3.7.4.3"], ConditionalDependencySpecifiers = ["python_version < \"3.8\""] }),
            ("Requires-Dist: TestPackage ; python_version < \"3.8\"", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [], ConditionalDependencySpecifiers = ["python_version < \"3.8\""] }),
            ("Requires-Dist: SpacePackage >=1.16.0", new PipDependencySpecification() { Name = "SpacePackage", DependencySpecifiers = [">=1.16.0"] }),
        };

        VerifyPipDependencyParsing(specs, true);
    }

    [TestMethod]
    public void TestPipDependencyRequireDistConditionalDependenciesMet()
    {
        var specs = new List<(string, bool, PipDependencySpecification)>
        {
            ("Requires-Dist: TestPackage (>=1.0.0) ; sys_platform == \"win32\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=1.0.0"], ConditionalDependencySpecifiers = ["sys_platform == \"win32\""] }),
            ("Requires-Dist: TestPackage (>=3.7.4.3) ; python_version < \"3.8\"", false, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=3.7.4.3"], ConditionalDependencySpecifiers = ["python_version < \"3.8\""] }),
            ("Requires-Dist: TestPackage ; python_version == \"3.8\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [], ConditionalDependencySpecifiers = ["python_version == \"3.8\""] }),
            ("Requires-Dist: TestPackage (>=3.0.1) ; python_version < \"3.5\" or sys_platform == \"win32\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=3.0.1"], ConditionalDependencySpecifiers = ["python_version < \"3.5\"", "or sys_platform == \"win32\""] }),
            ("Requires-Dist: TestPackage (>=2.0.1) ; python_version == \"3.8\" and sys_platform == \"win32\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=2.0.1"], ConditionalDependencySpecifiers = ["python_version == \"3.8\"", "and sys_platform == \"win32\""] }),
            ("Requires-Dist: TestPackage (>=2.0.1) ; python_version == \"3.8\" and sys_platform == \"linux\"", false, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=2.0.1"], ConditionalDependencySpecifiers = ["python_version == \"3.8\"", "and sys_platform == \"linux\""] }),
            ("Requires-Dist: TestPackage (>=4.0.1) ; python_version < \"3.6\" and sys_platform == \"win32\"", false, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=4.0.1"], ConditionalDependencySpecifiers = ["python_version < \"3.6\"", "and sys_platform == \"win32\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version > \"3.7\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["python_version > \"3.7\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version >= \"3.7\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["python_version >= \"3.7\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version < \"3.9\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["python_version < \"3.9\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version <= \"3.9\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["python_version <= \"3.9\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version == \"3.8\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["python_version == \"3.8\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version === \"3.8\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["python_version === \"3.8\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version ~= \"3.8\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["python_version ~= \"3.8\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version != \"3.8\"", false, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["python_version != \"3.8\""] }),
        };
        var pythonEnvironmentVariables = new Dictionary<string, string>
        {
            { "python_version", "3.8" },
            { "sys_platform", "win32" },
        };

        VerifyPipConditionalDependencyParsing(specs, pythonEnvironmentVariables, true);
    }

    [TestMethod]
    public void TestPipDependencyRequireDistConditionalDependenciesMet_Linux()
    {
        var specs = new List<(string, bool, PipDependencySpecification)>
        {
            ("Requires-Dist: TestPackage (>=2.0.1) ; python_version == \"3.8\" and sys_platform == \"linux\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=2.0.1"], ConditionalDependencySpecifiers = ["python_version == \"3.8\"", "and sys_platform == \"linux\""] }),
            ("Requires-Dist: TestPackage (>=4.0.1) ; python_version == \"3.6\" and sys_platform == \"win32\"", false, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=4.0.1"], ConditionalDependencySpecifiers = ["python_version == \"3.6\"", "and sys_platform == \"win32\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; sys_platform == \"linux\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["sys_platform == \"linux\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; sys_platform == \"win32\"", false, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["sys_platform == \"win32\""] }),
        };
        var pythonEnvironmentVariables = new Dictionary<string, string>
        {
            { "python_version", "3.8" },
            { "sys_platform", "linux" },
        };

        VerifyPipConditionalDependencyParsing(specs, pythonEnvironmentVariables, true);
    }

    [TestMethod]
    public void TestPipDependencyRequireDistConditionalDependenciesMet_Empty()
    {
        var specs = new List<(string, bool, PipDependencySpecification)>
        {
            ("Requires-Dist: TestPackage (>=2.0.1) ; python_version == \"3.8\" and sys_platform == \"linux\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=2.0.1"], ConditionalDependencySpecifiers = ["python_version == \"3.8\"", "and sys_platform == \"linux\""] }),
            ("Requires-Dist: TestPackage (>=4.0.1) ; python_version == \"3.6\" and sys_platform == \"win32\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=4.0.1"], ConditionalDependencySpecifiers = ["python_version == \"3.6\"", "and sys_platform == \"win32\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; sys_platform == \"linux\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["sys_platform == \"linux\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; sys_platform == \"win32\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["sys_platform == \"win32\""] }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; sys_platform == \"asdf\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = [">=5.0.1"], ConditionalDependencySpecifiers = ["sys_platform == \"asdf\""] }),
        };

        // test null and empty cases should allow packages through
        var pythonEnvironmentVariables = new Dictionary<string, string>
        {
            { "python_version", null },
            { "sys_platform", string.Empty },
        };

        VerifyPipConditionalDependencyParsing(specs, pythonEnvironmentVariables, true);
    }

    [TestMethod]
    public void TestPipDependencyGetHighestExplicitPackageVersion_Valid()
    {
        var spec = new PipDependencySpecification
        {
            Name = "TestPackage",
            DependencySpecifiers = [">=1.0", "<=3.0", "!=2.0", "!=4.0"],
        };

        var highestVersion = spec.GetHighestExplicitPackageVersion();
        highestVersion.Should().Be("3.0");
    }

    [TestMethod]
    public void TestPipDependencyGetHighestExplicitPackageVersion_SingleInvalidSpec()
    {
        var spec = new PipDependencySpecification
        {
            Name = "TestPackage",
            DependencySpecifiers = [">=1.0", "info", "!=2.0", "!=4.0"],
        };

        var highestVersion = spec.GetHighestExplicitPackageVersion();
        highestVersion.Should().BeNull();
    }

    [TestMethod]
    public void TestPipDependencyGetHighestExplicitPackageVersion_AllInvalidSpec()
    {
        var spec = new PipDependencySpecification
        {
            Name = "TestPackage",
            DependencySpecifiers = ["info"],
        };

        var highestVersion = spec.GetHighestExplicitPackageVersion();
        highestVersion.Should().BeNull();
    }
}
