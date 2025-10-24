#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using FluentAssertions;
    using Microsoft.ComponentDetection.Detectors.Uv;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Tomlyn.Model;

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

        [TestMethod]
        public void Parse_EmptyStream_ReturnsNoPackages()
        {
            using var ms = new MemoryStream([]);
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().BeEmpty();
        }

        [TestMethod]
        public void Parse_TomlNotATable_ThrowsException()
        {
            var toml = "42";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            FluentActions.Invoking(() => UvLock.Parse(ms))
                .Should().Throw<Exception>();
        }

        [TestMethod]
        public void Parse_NoPackageKey_ReturnsNoPackages()
        {
            var toml = "[metadata]\nversion = '1'";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().BeEmpty();
        }

        [TestMethod]
        public void Parse_PackageKeyNotArray_ReturnsNoPackages()
        {
            var toml = "package = 42";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().BeEmpty();
        }

        [TestMethod]
        public void Parse_PackageMissingNameOrVersion_IgnoresPackage()
        {
            var toml = @"
[[package]]
version = '1.2.3'
[[package]]
name = 'foo'
";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().BeEmpty();
        }

        [TestMethod]
        public void Parse_PackageWithMalformedDependencies_IgnoresMalformed()
        {
            var toml = @"
[[package]]
name = 'foo'
version = '1.2.3'
dependencies = [42, { name = 'bar' }]
";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().ContainSingle();
            var pkg = uvLock.Packages.First();
            pkg.Dependencies.Should().ContainSingle(d => d.Name == "bar");
        }

        [TestMethod]
        public void Parse_PackageWithMalformedMetadata_IgnoresMalformed()
        {
            var toml = @"
[[package]]
name = 'foo'
version = '1.2.3'
[package.metadata]
requires-dist = [42, { name = 'bar' }]
[package.metadata.requires-dev]
dev = [42, { name = 'baz' }]
";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().ContainSingle();
            var pkg = uvLock.Packages.First();
            pkg.MetadataRequiresDist.Should().ContainSingle(d => d.Name == "bar");
            pkg.MetadataRequiresDev.Should().ContainSingle(d => d.Name == "baz");
        }

        [TestMethod]
        public void ParsePackagesFromModel_InvalidRoot_Throws()
        {
            Action act = () => UvLock.ParsePackagesFromModel(42);
            act.Should().Throw<InvalidOperationException>();
        }

        [TestMethod]
        public void ParsePackagesFromModel_NoPackages_ReturnsEmpty()
        {
            var table = new TomlTable();
            var result = UvLock.ParsePackagesFromModel(table);
            result.Should().BeEmpty();
        }

        [TestMethod]
        public void ParsePackage_ValidPackage_ParsesCorrectly()
        {
            var pkg = new TomlTable
            {
                ["name"] = "foo",
                ["version"] = "1.0.0",
                ["dependencies"] = new TomlArray { new TomlTable { ["name"] = "bar", ["specifier"] = ">=2.0.0" } },
            };
            var result = UvLock.ParsePackage(pkg);
            result.Should().NotBeNull();
            result.Name.Should().Be("foo");
            result.Version.Should().Be("1.0.0");
            result.Dependencies.Should().ContainSingle(d => d.Name == "bar" && d.Specifier == ">=2.0.0");
        }

        [TestMethod]
        public void ParsePackage_MissingNameOrVersion_ReturnsNull()
        {
            var pkg1 = new TomlTable { ["version"] = "1.0.0" };
            var pkg2 = new TomlTable { ["name"] = "foo" };
            UvLock.ParsePackage(pkg1).Should().BeNull();
            UvLock.ParsePackage(pkg2).Should().BeNull();
        }

        [TestMethod]
        public void ParsePackage_NullOrNonTable_ReturnsNull()
        {
            UvLock.ParsePackage(null).Should().BeNull();
            UvLock.ParsePackage(42).Should().BeNull();
        }

        [TestMethod]
        public void ParsePackage_BranchCoverage_AllPaths()
        {
            // Path: pkg is TomlTable, but missing name
            var pkgMissingName = new TomlTable { ["version"] = "1.0.0" };
            UvLock.ParsePackage(pkgMissingName).Should().BeNull();

            // Path: pkg is TomlTable, but missing version
            var pkgMissingVersion = new TomlTable { ["name"] = "foo" };
            UvLock.ParsePackage(pkgMissingVersion).Should().BeNull();
        }

        [TestMethod]
        public void ParseDependenciesArray_ParsesValidDepsAndSkipsMalformed()
        {
            var arr = new TomlArray { 42, new TomlTable { ["name"] = "bar", ["specifier"] = "==1.2.3" }, new TomlTable { ["name"] = "baz" } };
            var result = UvLock.ParseDependenciesArray(arr);
            result.Should().Contain(d => d.Name == "bar" && d.Specifier == "==1.2.3");
            result.Should().Contain(d => d.Name == "baz" && d.Specifier == null);
            result.Should().HaveCount(2);
        }

        [TestMethod]
        public void ParseDependenciesArray_NullOrNoValidDeps_ReturnsEmpty()
        {
            UvLock.ParseDependenciesArray(null).Should().BeEmpty();
            var arr = new TomlArray { 42, "foo", 3.14 };
            UvLock.ParseDependenciesArray(arr).Should().BeEmpty();
        }

        [TestMethod]
        public void ParseDependenciesArray_BranchCoverage_AllPaths()
        {
            // Path: dep is TomlTable but missing name
            var arr = new TomlArray { new TomlTable { ["specifier"] = "==1.2.3" } };
            UvLock.ParseDependenciesArray(arr).Should().BeEmpty();
        }

        [TestMethod]
        public void ParseMetadata_ParsesRequiresDistAndDev()
        {
            var pkg = new UvPackage { Name = "foo", Version = "1.0.0" };
            var metadata = new TomlTable
            {
                ["requires-dist"] = new TomlArray { new TomlTable { ["name"] = "bar", ["specifier"] = ">=2.0.0" } },
                ["requires-dev"] = new TomlTable { ["dev"] = new TomlArray { new TomlTable { ["name"] = "baz" } } },
            };
            UvLock.ParseMetadata(metadata, pkg);
            pkg.MetadataRequiresDist.Should().ContainSingle(d => d.Name == "bar" && d.Specifier == ">=2.0.0");
            pkg.MetadataRequiresDev.Should().ContainSingle(d => d.Name == "baz" && d.Specifier == null);
        }

        [TestMethod]
        public void ParseMetadata_NullOrNoRelevantKeys_DoesNothing()
        {
            var pkg = new UvPackage { Name = "foo", Version = "1.0.0" };
            UvLock.ParseMetadata(null, pkg); // Should not throw
            var emptyTable = new TomlTable();
            UvLock.ParseMetadata(emptyTable, pkg); // Should not throw or set anything
            pkg.MetadataRequiresDist.Should().BeEmpty();
            pkg.MetadataRequiresDev.Should().BeEmpty();
        }

        [TestMethod]
        public void ParseMetadata_BranchCoverage_RequiresDistOnly()
        {
            var pkg = new UvPackage { Name = "foo", Version = "1.0.0" };
            var metadata = new TomlTable
            {
                ["requires-dist"] = new TomlArray { new TomlTable { ["name"] = "bar" } },
            };
            UvLock.ParseMetadata(metadata, pkg);
            pkg.MetadataRequiresDist.Should().ContainSingle(d => d.Name == "bar");
            pkg.MetadataRequiresDev.Should().BeEmpty();
        }

        [TestMethod]
        public void ParseMetadata_BranchCoverage_RequiresDevOnly()
        {
            var pkg = new UvPackage { Name = "foo", Version = "1.0.0" };
            var metadata = new TomlTable
            {
                ["requires-dev"] = new TomlTable { ["dev"] = new TomlArray { new TomlTable { ["name"] = "baz" } } },
            };
            UvLock.ParseMetadata(metadata, pkg);
            pkg.MetadataRequiresDist.Should().BeEmpty();
            pkg.MetadataRequiresDev.Should().ContainSingle(d => d.Name == "baz");
        }

        [TestMethod]
        public void ParseMetadata_RequiresDevTableWithoutDevArray_DoesNotThrowOrSet()
        {
            var pkg = new UvPackage { Name = "foo", Version = "1.0.0" };

            // requires-dev exists but no "dev" key
            var metadata = new TomlTable
            {
                ["requires-dev"] = new TomlTable { ["notdev"] = 42 },
            };
            UvLock.ParseMetadata(metadata, pkg);
            pkg.MetadataRequiresDev.Should().BeEmpty();

            // requires-dev exists, "dev" is not a TomlArray
            metadata = new TomlTable
            {
                ["requires-dev"] = new TomlTable { ["dev"] = 42 },
            };
            UvLock.ParseMetadata(metadata, pkg);
            pkg.MetadataRequiresDev.Should().BeEmpty();
        }

        [TestMethod]
        public void ParsePackage_ParsesSourceRegistryAndVirtual()
        {
            var toml = """
[[package]]
name = 'foo'
version = '1.0.0'
source = { registry = 'https://example.com/', virtual = '.' }
""";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().ContainSingle();
            var pkg = uvLock.Packages.First();
            pkg.Source.Should().NotBeNull();
            pkg.Source!.Registry.Should().Be("https://example.com/");
            pkg.Source.Virtual.Should().Be(".");
        }

        [TestMethod]
        public void ParsePackage_ParsesSource_RegistryOnly()
        {
            var toml = """
[[package]]
name = 'foo'
version = '1.0.0'
source = { registry = 'https://example.com/' }
""";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().ContainSingle();
            var pkg = uvLock.Packages.First();
            pkg.Source.Should().NotBeNull();
            pkg.Source!.Registry.Should().Be("https://example.com/");
            pkg.Source.Virtual.Should().BeNull();
        }

        [TestMethod]
        public void ParsePackage_ParsesSource_VirtualOnly()
        {
            var toml = """
[[package]]
name = 'foo'
version = '1.0.0'
source = { virtual = '.' }
""";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().ContainSingle();
            var pkg = uvLock.Packages.First();
            pkg.Source.Should().NotBeNull();
            pkg.Source!.Registry.Should().BeNull();
            pkg.Source.Virtual.Should().Be(".");
        }

        [TestMethod]
        public void ParsePackage_ParsesSource_Missing()
        {
            var toml = """
[[package]]
name = 'foo'
version = '1.0.0'
""";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(toml));
            var uvLock = UvLock.Parse(ms);
            uvLock.Packages.Should().ContainSingle();
            var pkg = uvLock.Packages.First();
            pkg.Source.Should().BeNull();
        }
    }
}
