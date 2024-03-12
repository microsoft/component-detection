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
    public void DeserializePnpmYamlFile()
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

        var parsedYaml = PnpmParsingUtilities.DeserializePnpmYamlV5File(yamlFile);

        parsedYaml.packages.Should().HaveCount(2);
        parsedYaml.packages.Should().ContainKey("/query-string/4.3.4");
        parsedYaml.packages.Should().ContainKey("/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2");

        var queryStringPackage = parsedYaml.packages["/query-string/4.3.4"];
        queryStringPackage.dependencies.Should().ContainSingle();
        queryStringPackage.dependencies.Should().ContainKey("@ms/items-view");
        queryStringPackage.dependencies["@ms/items-view"].Should().BeEquivalentTo("/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2");
        queryStringPackage.dev.Should().BeEquivalentTo("false");

        var itemViewPackage = parsedYaml.packages["/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2"];
        itemViewPackage.dependencies.Should().BeNull();
        itemViewPackage.dev.Should().BeEquivalentTo("true");
    }

    [TestMethod]
    public void CreateDetectedComponentFromPnpmPathV5()
    {
        var detectedComponent1 = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV5("/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2");
        detectedComponent1.Should().NotBeNull();
        detectedComponent1.Component.Should().NotBeNull();
        ((NpmComponent)detectedComponent1.Component).Name.Should().BeEquivalentTo("@ms/items-view");
        ((NpmComponent)detectedComponent1.Component).Version.Should().BeEquivalentTo("0.128.9");

        var detectedComponent2 = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV5("/@babel/helper-compilation-targets/7.10.4_@babel+core@7.10.5");
        detectedComponent2.Should().NotBeNull();
        detectedComponent2.Component.Should().NotBeNull();
        ((NpmComponent)detectedComponent2.Component).Name.Should().BeEquivalentTo("@babel/helper-compilation-targets");
        ((NpmComponent)detectedComponent2.Component).Version.Should().BeEquivalentTo("7.10.4");

        var detectedComponent3 = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV5("/query-string/4.3.4");
        detectedComponent3.Should().NotBeNull();
        detectedComponent3.Component.Should().NotBeNull();
        ((NpmComponent)detectedComponent3.Component).Name.Should().BeEquivalentTo("query-string");
        ((NpmComponent)detectedComponent3.Component).Version.Should().BeEquivalentTo("4.3.4");
    }

    [TestMethod]
    public void CreateDetectedComponentFromPnpmPathV6()
    {
        // Simple case: no scope, simple version
        var simple = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV6("/sort-scripts@1.0.1");
        ((NpmComponent)simple.Component).Name.Should().BeEquivalentTo("sort-scripts");
        ((NpmComponent)simple.Component).Version.Should().BeEquivalentTo("1.0.1");

        // With scope:
        var scoped = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV6("/@babel/eslint-parser@7.23.3");
        ((NpmComponent)scoped.Component).Name.Should().BeEquivalentTo("@babel/eslint-parser");
        ((NpmComponent)scoped.Component).Version.Should().BeEquivalentTo("7.23.3");

        // With peer deps:
        var withPeerDeps = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV6("/mocha-json-output-reporter@2.1.0(mocha@10.2.0)(moment@2.29.4)");
        ((NpmComponent)withPeerDeps.Component).Name.Should().BeEquivalentTo("mocha-json-output-reporter");
        ((NpmComponent)withPeerDeps.Component).Version.Should().BeEquivalentTo("2.1.0");

        // With everything:
        var complex = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPathV6("/@babel/eslint-parser@7.23.3(@babel/core@7.23.3)(eslint@8.55.0)");
        ((NpmComponent)complex.Component).Name.Should().BeEquivalentTo("@babel/eslint-parser");
        ((NpmComponent)complex.Component).Version.Should().BeEquivalentTo("7.23.3");
    }

    [TestMethod]
    public void ReconstructPnpmDependencyPathV6()
    {
        // Simple case: no scope, simple version
        PnpmParsingUtilities.ReconstructPnpmDependencyPathV6("sort-scripts", "1.0.1").Should().BeEquivalentTo("/sort-scripts@1.0.1");

        // With scope:
        PnpmParsingUtilities.ReconstructPnpmDependencyPathV6("@babel/eslint-parser", "7.23.3").Should().BeEquivalentTo("/@babel/eslint-parser@7.23.3");

        // With peer deps:
        PnpmParsingUtilities.ReconstructPnpmDependencyPathV6("mocha-json-output-reporter", "2.1.0(mocha@10.2.0)(moment@2.29.4)").Should().BeEquivalentTo("/mocha-json-output-reporter@2.1.0(mocha@10.2.0)(moment@2.29.4)");

        // Absolute path:
        PnpmParsingUtilities.ReconstructPnpmDependencyPathV6("events_pkg", "/events@3.3.0").Should().BeEquivalentTo("/events@3.3.0");
    }

    [TestMethod]
    public void IsPnpmPackageDevDependency()
    {
        var pnpmPackage = new Package
        {
            dev = "true",
        };

        PnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeTrue();

        pnpmPackage.dev = "TRUE";
        PnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeTrue();

        pnpmPackage.dev = "false";
        PnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeFalse();

        pnpmPackage.dev = "FALSE";
        PnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeFalse();

        pnpmPackage.dev = string.Empty;
        PnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeFalse();

        pnpmPackage.dev = null;
        PnpmParsingUtilities.IsPnpmPackageDevDependency(pnpmPackage).Should().BeFalse();

        Action action = () => PnpmParsingUtilities.IsPnpmPackageDevDependency(null);
        action.Should().Throw<ArgumentNullException>();
    }
}
