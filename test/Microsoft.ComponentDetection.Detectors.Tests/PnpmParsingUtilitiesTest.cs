#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pnpm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PnpmParsingUtilitiesTest
{
    // This tests a version 3 shrink-wrap file as defined by https://github.com/pnpm/spec/blob/master/lockfile/3.md
    // This is handled by the "V5" code, which parses most of the data, but misses some things, like the shrinkwrapVersion.
    [TestMethod]
    public void DeserializePnpmYamlFileV3()
    {
        var yamlFile = @"
dependencies:
  'query-string': 4.3.4
packages:
  /query-string/4.3.4:
    dependencies:
      '@ms/items-view': /@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2
    dev: false
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-u7aTucqRXCMlFbIosaArYJBD2+s=
  /@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2:
    dev: true
    engines:
      node: '>=0.10.0'
    resolution:
      integrity: sha1-J5siXfHVgrH1TmWt3UNS4Y+qBxM=
registry: 'https://test/registry'
shrinkwrapMinorVersion: 7
shrinkwrapVersion: 3";
        var pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV5>();
        var version = PnpmParsingUtilitiesFactory.DeserializePnpmYamlFileVersion(yamlFile);
        version.Should().BeNull(); // Versions older than 5 report null as they don't use the same version field.
        var parsedYaml = pnpmParsingUtilities.DeserializePnpmYamlFile(yamlFile);

        parsedYaml.Packages.Should().HaveCount(2);
        parsedYaml.Packages.Should().ContainKey("/query-string/4.3.4");
        parsedYaml.Packages.Should().ContainKey("/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2");

        var queryStringPackage = parsedYaml.Packages["/query-string/4.3.4"];
        queryStringPackage.Dependencies.Should().ContainSingle();
        queryStringPackage.Dependencies.Should().ContainKey("@ms/items-view");
        queryStringPackage.Dependencies["@ms/items-view"].Should().BeEquivalentTo("/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2");
        queryStringPackage.Dev.Should().BeEquivalentTo("false");

        var itemViewPackage = parsedYaml.Packages["/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2"];
        itemViewPackage.Dependencies.Should().BeNull();
        itemViewPackage.Dev.Should().BeEquivalentTo("true");
    }

    [TestMethod]
    public void CreateDetectedComponentFromPnpmPathV5()
    {
        var pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV5>();
        var detectedComponent1 = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2");
        detectedComponent1.Should().NotBeNull();
        detectedComponent1.Component.Should().NotBeNull();
        ((NpmComponent)detectedComponent1.Component).Name.Should().BeEquivalentTo("@ms/items-view");
        ((NpmComponent)detectedComponent1.Component).Version.Should().BeEquivalentTo("0.128.9");

        var detectedComponent2 = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/@babel/helper-compilation-targets/7.10.4_@babel+core@7.10.5");
        detectedComponent2.Should().NotBeNull();
        detectedComponent2.Component.Should().NotBeNull();
        ((NpmComponent)detectedComponent2.Component).Name.Should().BeEquivalentTo("@babel/helper-compilation-targets");
        ((NpmComponent)detectedComponent2.Component).Version.Should().BeEquivalentTo("7.10.4");

        var detectedComponent3 = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/query-string/4.3.4");
        detectedComponent3.Should().NotBeNull();
        detectedComponent3.Component.Should().NotBeNull();
        ((NpmComponent)detectedComponent3.Component).Name.Should().BeEquivalentTo("query-string");
        ((NpmComponent)detectedComponent3.Component).Version.Should().BeEquivalentTo("4.3.4");
    }

    [TestMethod]
    public void IsPnpmPackageDevDependency()
    {
        var pnpmPackage = new Package
        {
            Dev = "true",
        };

        var pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV5>();

        pnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeTrue();

        pnpmPackage.Dev = "TRUE";
        pnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeTrue();

        pnpmPackage.Dev = "false";
        pnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeFalse();

        pnpmPackage.Dev = "FALSE";
        pnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeFalse();

        pnpmPackage.Dev = string.Empty;
        pnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeFalse();

        pnpmPackage.Dev = null;
        pnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeFalse();

        Action action = () => pnpmParsingUtilities.IsPnpmPackageDevDependency(null);
        action.Should().Throw<ArgumentNullException>();
    }

    // This tests a version 3 pnpm-lock.yaml file as defined by https://github.com/pnpm/spec/blob/master/lockfile/6.md.
    [TestMethod]
    public void DeserializePnpmYamlFileV6()
    {
        var yamlFile = @"
lockfileVersion: '6.0'
settings:
  autoInstallPeers: true
  excludeLinksFromLockfile: false
dependencies:
  minimist:
    specifier: 1.2.8
    version: 1.2.8
packages:
  /minimist@1.2.8:
    resolution: {integrity: sha512-2yyAR8qBkN3YuheJanUpWC5U3bb5osDywNB8RzDVlDwDHbocAJveqqj1u8+SVD7jkWT4yvsHCpWqqWqAxb0zCA==}
    dev: false";
        var pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV6>();
        var version = PnpmParsingUtilitiesFactory.DeserializePnpmYamlFileVersion(yamlFile);
        version.Should().Be("6.0");
        var parsedYaml = pnpmParsingUtilities.DeserializePnpmYamlFile(yamlFile);

        parsedYaml.Packages.Should().ContainSingle();
        parsedYaml.Packages.Should().ContainKey("/minimist@1.2.8");

        var package = parsedYaml.Packages["/minimist@1.2.8"];
        package.Dependencies.Should().BeNull();

        parsedYaml.Dependencies.Should().ContainSingle();
        parsedYaml.Dependencies.Should().ContainKey("minimist");
        parsedYaml.Dependencies["minimist"].Version.Should().BeEquivalentTo("1.2.8");
    }

    [TestMethod]
    public void CreateDetectedComponentFromPnpmPathV6()
    {
        var pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV6>();

        // Simple case: no scope, simple version
        var simple = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/sort-scripts@1.0.1");
        ((NpmComponent)simple.Component).Name.Should().BeEquivalentTo("sort-scripts");
        ((NpmComponent)simple.Component).Version.Should().BeEquivalentTo("1.0.1");

        // With scope:
        var scoped = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/@babel/eslint-parser@7.23.3");
        ((NpmComponent)scoped.Component).Name.Should().BeEquivalentTo("@babel/eslint-parser");
        ((NpmComponent)scoped.Component).Version.Should().BeEquivalentTo("7.23.3");

        // With peer deps:
        var withPeerDeps = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/mocha-json-output-reporter@2.1.0(mocha@10.2.0)(moment@2.29.4)");
        ((NpmComponent)withPeerDeps.Component).Name.Should().BeEquivalentTo("mocha-json-output-reporter");
        ((NpmComponent)withPeerDeps.Component).Version.Should().BeEquivalentTo("2.1.0");

        // With everything:
        var complex = pnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/@babel/eslint-parser@7.23.3(@babel/core@7.23.3)(eslint@8.55.0)");
        ((NpmComponent)complex.Component).Name.Should().BeEquivalentTo("@babel/eslint-parser");
        ((NpmComponent)complex.Component).Version.Should().BeEquivalentTo("7.23.3");
    }

    [TestMethod]
    public void ReconstructPnpmDependencyPathV6()
    {
        var pnpmParsingUtilities = PnpmParsingUtilitiesFactory.Create<PnpmYamlV6>();

        // Simple case: no scope, simple version
        pnpmParsingUtilities.ReconstructPnpmDependencyPath("sort-scripts", "1.0.1").Should().BeEquivalentTo("/sort-scripts@1.0.1");

        // With scope:
        pnpmParsingUtilities.ReconstructPnpmDependencyPath("@babel/eslint-parser", "7.23.3").Should().BeEquivalentTo("/@babel/eslint-parser@7.23.3");

        // With peer deps:
        pnpmParsingUtilities.ReconstructPnpmDependencyPath("mocha-json-output-reporter", "2.1.0(mocha@10.2.0)(moment@2.29.4)").Should().BeEquivalentTo("/mocha-json-output-reporter@2.1.0(mocha@10.2.0)(moment@2.29.4)");

        // Absolute path:
        pnpmParsingUtilities.ReconstructPnpmDependencyPath("events_pkg", "/events@3.3.0").Should().BeEquivalentTo("/events@3.3.0");
    }
}
