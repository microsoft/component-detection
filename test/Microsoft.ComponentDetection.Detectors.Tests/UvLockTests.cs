namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.Uv;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UvLockTests
{
    [TestMethod]
    public void Parse_ParsesMetadataRequiresDistAndDev()
    {
        var toml = """
[[package]]
name = "component-detection"
version = "0.0.0"

[package.metadata]
requires-dist = [
    { name = "azure-identity", specifier = "==1.17.1" },
    { name = "flask", specifier = ">2,<3" },
    { name = "requests", specifier = ">=2.32.0" },
]

[package.metadata.requires-dev]
dev = [
    { name = "pytest", specifier = ">=8.3.4" },
    { name = "pytest-cov", specifier = ">=6.0.0" },
    { name = "pytest-env", specifier = ">=1.1.5" },
]
""";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
        var uvLock = UvLock.Parse(ms);
        uvLock.Packages.Should().ContainSingle();

        var package = uvLock.Packages.First();
        package.Name.Should().Be("component-detection");
        package.Version.Should().Be("0.0.0");

        package.MetadataRequiresDist.Should().BeEquivalentTo(
            [
                new UvDependency { Name = "azure-identity", Specifier = "==1.17.1" },
                new UvDependency { Name = "flask", Specifier = ">2,<3" },
                new UvDependency { Name = "requests", Specifier = ">=2.32.0" },
            ],
            options => options.ComparingByMembers<UvDependency>());

        package.MetadataRequiresDev.Should().BeEquivalentTo(
            [
                new UvDependency { Name = "pytest", Specifier = ">=8.3.4" },
                new UvDependency { Name = "pytest-cov", Specifier = ">=6.0.0" },
                new UvDependency { Name = "pytest-env", Specifier = ">=1.1.5" },
            ],
            options => options.ComparingByMembers<UvDependency>());
    }

    [TestMethod]
    public void Parse_ParsesPackagesAndDependencies()
    {
        var toml = @"
[[package]]
name = 'foo'
version = '1.2.3'
dependencies = [
    { name = 'bar', specifier = '>=2.0.0' },
]
[[package]]
name = 'bar'
version = '2.0.0'
";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
        var uvLock = UvLock.Parse(ms);
        uvLock.Packages.Should().HaveCount(2);
        uvLock.Packages.First().Name.Should().Be("foo");
        uvLock.Packages.First().Dependencies.Should().ContainSingle(d => d.Name == "bar" && d.Specifier == ">=2.0.0");
    }
}
