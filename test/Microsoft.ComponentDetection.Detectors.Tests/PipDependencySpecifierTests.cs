using System.Collections.Generic;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    public class PipDependencySpecifierTests
    {
        [TestMethod]
        public void TestPipDependencySpecifierConstruction()
        {
            var specs = new List<(string, PipDependencySpecification)>
            {
                ("TestPackage==1.0", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { "==1.0" } }),
                ("TestPackage>=1.0,!=1.1", new PipDependencySpecification { Name = "TestPackage", DependencySpecifiers = new List<string> { ">=1.0", "!=1.1" } }),
                ("OtherPackage!=1.2,>=1.0,<=1.9,~=1.4", new PipDependencySpecification { Name = "OtherPackage", DependencySpecifiers = new List<string> { "!=1.2", ">=1.0", "<=1.9", "~=1.4" } }),
            };

            foreach (var spec in specs)
            {
                var (specString, referenceDependencySpecification) = spec;
                var dependencySpecifier = new PipDependencySpecification(specString);

                Assert.AreEqual(referenceDependencySpecification.Name, dependencySpecifier.Name);

                for (var i = 0; i < referenceDependencySpecification.DependencySpecifiers.Count; i++)
                {
                    Assert.AreEqual(referenceDependencySpecification.DependencySpecifiers[i], dependencySpecifier.DependencySpecifiers[i]);
                }
            }
        }
    }
}
