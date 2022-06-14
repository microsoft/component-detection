using FluentAssertions;
using FluentAssertions.Primitives;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Faker;
using static Microsoft.ComponentDetection.Common.DependencyScopeComparer;
using System.Linq;

namespace Microsoft.ComponentDetection.Common.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class DependencyScopeComparerTests
    {   
        [TestMethod]
        public void GetMergedDependencyScope_returnNull_IfBothNull()
        {
            GetMergedDependencyScope(null, null).Should().BeNull();
        }

        [TestMethod]
        public void GetMergedDependencyScope_returnSecondIfFirstNulll()
        {
            DependencyScope randomDependencyScope = Faker.Enum.Random<DependencyScope>();
            GetMergedDependencyScope(null, randomDependencyScope)
                .Should()
                .Equals(randomDependencyScope);
        }

        [TestMethod]
        public void GetMergedDependencyScope_returnFirstIfSecondNulll()
        {
            DependencyScope randomDependencyScope = Faker.Enum.Random<DependencyScope>();
            GetMergedDependencyScope(randomDependencyScope, null)
                .Should()
                .Equals(randomDependencyScope);
        }

        [TestMethod]
        public void GetMergedDependencyScope_WhenBothNonNull_higherPriorityEnumsReturned()
        {
            // compare with compile
            GetMergedDependencyScope(DependencyScope.MavenRuntime, DependencyScope.MavenCompile).Should().Equals(DependencyScope.MavenCompile);
            GetMergedDependencyScope(DependencyScope.MavenProvided, DependencyScope.MavenCompile).Should().Equals(DependencyScope.MavenCompile);
            GetMergedDependencyScope(DependencyScope.MavenSystem, DependencyScope.MavenCompile).Should().Equals(DependencyScope.MavenCompile);
            GetMergedDependencyScope(DependencyScope.MavenTest, DependencyScope.MavenCompile).Should().Equals(DependencyScope.MavenCompile);

            // compare with runtime
            GetMergedDependencyScope(DependencyScope.MavenProvided, DependencyScope.MavenRuntime).Should().Equals(DependencyScope.MavenRuntime);
            GetMergedDependencyScope(DependencyScope.MavenSystem, DependencyScope.MavenRuntime).Should().Equals(DependencyScope.MavenRuntime);
            GetMergedDependencyScope(DependencyScope.MavenTest, DependencyScope.MavenRuntime).Should().Equals(DependencyScope.MavenRuntime);

            // compare with provided
            GetMergedDependencyScope(DependencyScope.MavenSystem, DependencyScope.MavenProvided).Should().Equals(DependencyScope.MavenProvided);
            GetMergedDependencyScope(DependencyScope.MavenTest, DependencyScope.MavenProvided).Should().Equals(DependencyScope.MavenProvided);

            // compare with system
            GetMergedDependencyScope(DependencyScope.MavenSystem, DependencyScope.MavenTest).Should().Equals(DependencyScope.MavenSystem);
        }
    }
}
