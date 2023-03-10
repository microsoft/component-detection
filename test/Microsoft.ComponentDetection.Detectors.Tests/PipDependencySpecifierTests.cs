namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
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

            Assert.AreEqual(referenceDependencySpecification.Name, dependencySpecifier.Name);

            for (var i = 0; i < referenceDependencySpecification.DependencySpecifiers.Count; i++)
            {
                Assert.AreEqual(
                    referenceDependencySpecification.DependencySpecifiers[i],
                    dependencySpecifier.DependencySpecifiers[i]);
            }
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
            ("Requires-Dist: TestPackage (>=1.0.0) ; sys_platform == \"win32\"", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=1.0.0" } }),
            ("Requires-Dist: OtherPackage[Optional] (<3,>=1.0.0)", new PipDependencySpecification { Name = "OtherPackage", DependencySpecifiers = new List<string> { "<3", ">=1.0.0" } }),
            ("Requires-Dist: TestPackage (>=3.7.4.3) ; python_version < \"3.8\"", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=3.7.4.3" } }),
            ("Requires-Dist: TestPackage ; python_version < \"3.8\"", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string>() }),
        };

        VerifyPipDependencyParsing(specs, true);
    }
}
