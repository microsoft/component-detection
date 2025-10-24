#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PathUtilityServiceTests
{
    [TestMethod]
    public void PathShouldBeNormalized()
    {
        var service = new PathUtilityService(new NullLogger<PathUtilityService>());
        var path = "Users\\SomeUser\\someDir\\someFile";
        var expectedPath = "Users/SomeUser/someDir/someFile";
        var normalizedPath = service.NormalizePath(path);

        normalizedPath.Should().Be(expectedPath);
    }

    [TestMethod]
    public void AbsolutePathShouldBeNormalized()
    {
        var service = new PathUtilityService(new NullLogger<PathUtilityService>());
        var path = "C:\\Users\\SomeUser\\someDir\\someFile";
        var expectedPath = "C:/Users/SomeUser/someDir/someFile";
        var normalizedPath = service.NormalizePath(path);

        normalizedPath.Should().Be(expectedPath);
    }
}
