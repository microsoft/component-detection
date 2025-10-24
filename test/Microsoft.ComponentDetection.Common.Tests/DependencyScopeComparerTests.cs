#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.ComponentDetection.Common.DependencyScopeComparer;

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
        var randomDependencyScope = Faker.Enum.Random<DependencyScope>();
        GetMergedDependencyScope(null, randomDependencyScope)
            .Should()
            .Be(randomDependencyScope);
    }

    [TestMethod]
    public void GetMergedDependencyScope_returnFirstIfSecondNulll()
    {
        var randomDependencyScope = Faker.Enum.Random<DependencyScope>();
        GetMergedDependencyScope(randomDependencyScope, null)
            .Should()
            .Be(randomDependencyScope);
    }

    [TestMethod]
    public void GetMergedDependencyScope_WhenBothNonNull_higherPriorityEnumsReturned()
    {
        // compare with compile
        GetMergedDependencyScope(DependencyScope.MavenRuntime, DependencyScope.MavenCompile).Should().Be(DependencyScope.MavenCompile);
        GetMergedDependencyScope(DependencyScope.MavenProvided, DependencyScope.MavenCompile).Should().Be(DependencyScope.MavenCompile);
        GetMergedDependencyScope(DependencyScope.MavenSystem, DependencyScope.MavenCompile).Should().Be(DependencyScope.MavenCompile);
        GetMergedDependencyScope(DependencyScope.MavenTest, DependencyScope.MavenCompile).Should().Be(DependencyScope.MavenCompile);

        // compare with runtime
        GetMergedDependencyScope(DependencyScope.MavenProvided, DependencyScope.MavenRuntime).Should().Be(DependencyScope.MavenRuntime);
        GetMergedDependencyScope(DependencyScope.MavenSystem, DependencyScope.MavenRuntime).Should().Be(DependencyScope.MavenRuntime);
        GetMergedDependencyScope(DependencyScope.MavenTest, DependencyScope.MavenRuntime).Should().Be(DependencyScope.MavenRuntime);

        // compare with provided
        GetMergedDependencyScope(DependencyScope.MavenSystem, DependencyScope.MavenProvided).Should().Be(DependencyScope.MavenProvided);
        GetMergedDependencyScope(DependencyScope.MavenTest, DependencyScope.MavenProvided).Should().Be(DependencyScope.MavenProvided);

        // compare with system
        GetMergedDependencyScope(DependencyScope.MavenSystem, DependencyScope.MavenTest).Should().Be(DependencyScope.MavenSystem);
    }
}
