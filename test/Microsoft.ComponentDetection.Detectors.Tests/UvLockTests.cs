namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
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
        var toml = @"
[package.metadata]
requires-dist = [
    { name = 'foo', specifier = '==1.0.0' },
    { name = 'bar' },
]
[package.metadata.requires-dev]
dev = [
    { name = 'pytest', specifier = '>=8.3.4' },
    { name = 'pytest-cov', specifier = '>=6.0.0' },
    { name = 'pytest-env', specifier = '>=1.1.5' },
]
";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
        var uvLock = UvLock.Parse(ms);
        uvLock.MetadataRequiresDist.Should().BeEquivalentTo(
            new List<UvDependency>
            {
                new UvDependency { Name = "foo", Specifier = "==1.0.0" },
                new UvDependency { Name = "bar", Specifier = null },
            },
            options => options.ComparingByMembers<UvDependency>());
        uvLock.MetadataRequiresDev.Should().BeEquivalentTo(
            new List<UvDependency>
            {
                new UvDependency { Name = "pytest", Specifier = ">=8.3.4" },
                new UvDependency { Name = "pytest-cov", Specifier = ">=6.0.0" },
                new UvDependency { Name = "pytest-env", Specifier = ">=1.1.5" },
            },
            options => options.ComparingByMembers<UvDependency>());
    }

    [TestMethod]
    public void Parse_EmptyMetadataLists()
    {
        var toml = @"
[package.metadata]
[package.metadata.requires-dev]
";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
        var uvLock = UvLock.Parse(ms);
        uvLock.MetadataRequiresDist.Should().BeEmpty();
        uvLock.MetadataRequiresDev.Should().BeEmpty();
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
