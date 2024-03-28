namespace Microsoft.ComponentDetection.Common.Tests;

using System.IO;
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
        var normalizedPath = service.NormalizePath(path);
        if (Path.DirectorySeparatorChar == '\\')
        {
            normalizedPath.Should().Be(path);
        }
        else
        {
            normalizedPath.Should().Be(string.Join(Path.DirectorySeparatorChar, path.Split('\\')));
        }
    }
}
