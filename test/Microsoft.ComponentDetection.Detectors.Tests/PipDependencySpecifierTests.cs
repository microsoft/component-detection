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
            ("TestPackage==1.0", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { "==1.0" } }),
            ("TestPackage>=1.0,!=1.1", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=1.0", "!=1.1" } }),
            ("OtherPackage!=1.2,>=1.0,<=1.9,~=1.4", new PipDependencySpecification { Name = "OtherPackage", DependencySpecifiers = new List<string> { "!=1.2", ">=1.0", "<=1.9", "~=1.4" } }),
            ("TestPackage[Optional]<3,>=1.0.0", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { "<3", ">=1.0.0" } }),
        };

        VerifyPipDependencyParsing(specs);
    }

    [TestMethod]
    public void TestPipDependencyRequireDist()
    {
        var specs = new List<(string, PipDependencySpecification)>
        {
            ("Requires-Dist: TestPackage<1.27.0,>=1.19.5", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { "<1.27.0", ">=1.19.5" } }),
            ("Requires-Dist: TestPackage (>=1.0.0) ; sys_platform == \"win32\"", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=1.0.0" }, ConditionalDependencySpecifiers = new List<string> { "sys_platform == \"win32\"" } }),
            ("Requires-Dist: OtherPackage[Optional] (<3,>=1.0.0)", new PipDependencySpecification { Name = "OtherPackage", DependencySpecifiers = new List<string> { "<3", ">=1.0.0" } }),
            ("Requires-Dist: TestPackage (>=3.7.4.3) ; python_version < \"3.8\"", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=3.7.4.3" }, ConditionalDependencySpecifiers = new List<string> { "python_version < \"3.8\"" } }),
            ("Requires-Dist: TestPackage ; python_version < \"3.8\"", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string>(), ConditionalDependencySpecifiers = new List<string> { "python_version < \"3.8\"" } }),
            ("Requires-Dist: SpacePackage >=1.16.0", new PipDependencySpecification() { Name = "SpacePackage", DependencySpecifiers = new List<string>() { ">=1.16.0" } }),
        };

        VerifyPipDependencyParsing(specs, true);
    }

    [TestMethod]
    public void TestPipDependencyRequireDistConditionalDependenciesMet()
    {
        var specs = new List<(string, bool, PipDependencySpecification)>
        {
            ("Requires-Dist: TestPackage (>=1.0.0) ; sys_platform == \"win32\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=1.0.0" }, ConditionalDependencySpecifiers = new List<string> { "sys_platform == \"win32\"" } }),
            ("Requires-Dist: TestPackage (>=3.7.4.3) ; python_version < \"3.8\"", false, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=3.7.4.3" }, ConditionalDependencySpecifiers = new List<string> { "python_version < \"3.8\"" } }),
            ("Requires-Dist: TestPackage ; python_version == \"3.8\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string>(), ConditionalDependencySpecifiers = new List<string> { "python_version == \"3.8\"" } }),
            ("Requires-Dist: TestPackage (>=3.0.1) ; python_version < \"3.5\" or sys_platform == \"win32\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=3.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version < \"3.5\"", "or sys_platform == \"win32\"" } }),
            ("Requires-Dist: TestPackage (>=2.0.1) ; python_version == \"3.8\" and sys_platform == \"win32\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=2.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version == \"3.8\"", "and sys_platform == \"win32\"" } }),
            ("Requires-Dist: TestPackage (>=4.0.1) ; python_version < \"3.6\" and sys_platform == \"win32\"", false, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=4.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version < \"3.6\"", "and sys_platform == \"win32\"" } }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version > \"3.7\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=5.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version > \"3.7\"" } }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version >= \"3.7\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=5.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version >= \"3.7\"" } }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version < \"3.9\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=5.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version < \"3.9\"" } }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version <= \"3.9\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=5.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version <= \"3.9\"" } }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version == \"3.8\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=5.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version == \"3.8\"" } }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version === \"3.8\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=5.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version === \"3.8\"" } }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version ~= \"3.8\"", true, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=5.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version ~= \"3.8\"" } }),
            ("Requires-Dist: TestPackage (>=5.0.1) ; python_version != \"3.8\"", false, new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=5.0.1" }, ConditionalDependencySpecifiers = new List<string> { "python_version != \"3.8\"" } }),
        };
        var pythonEnvironmentVariables = new Dictionary<string, string>
        {
            { "python_version", "3.8" },
            { "sys_platform", "win32" },
        };

        VerifyPipConditionalDependencyParsing(specs, pythonEnvironmentVariables, true);
    }
}
