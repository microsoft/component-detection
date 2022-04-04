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
            GetMergedDependencyScope(DependencyScope.Runtime, DependencyScope.Compile).Should().Equals(DependencyScope.Compile);
            GetMergedDependencyScope(DependencyScope.Provided, DependencyScope.Compile).Should().Equals(DependencyScope.Compile);
            GetMergedDependencyScope(DependencyScope.System, DependencyScope.Compile).Should().Equals(DependencyScope.Compile);
            GetMergedDependencyScope(DependencyScope.Test, DependencyScope.Compile).Should().Equals(DependencyScope.Compile);

            // compare with runtime
            GetMergedDependencyScope(DependencyScope.Provided, DependencyScope.Runtime).Should().Equals(DependencyScope.Runtime);
            GetMergedDependencyScope(DependencyScope.System, DependencyScope.Runtime).Should().Equals(DependencyScope.Runtime);
            GetMergedDependencyScope(DependencyScope.Test, DependencyScope.Runtime).Should().Equals(DependencyScope.Runtime);

            // compare with provided
            GetMergedDependencyScope(DependencyScope.System, DependencyScope.Provided).Should().Equals(DependencyScope.Provided);
            GetMergedDependencyScope(DependencyScope.Test, DependencyScope.Provided).Should().Equals(DependencyScope.Provided);

            // compare with system
            GetMergedDependencyScope(DependencyScope.System, DependencyScope.Test).Should().Equals(DependencyScope.System);
        }
    }
}
