namespace Microsoft.ComponentDetection.Detectors.Tests;
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Pnpm;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
public class PnpmParsingUtilitiesTest
{
    [TestMethod]
    public async Task DeserializePnpmYamlFileAsync()
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

        var parsedYaml = await PnpmParsingUtilities.DeserializePnpmYamlFileAsync(CreateComponentStreamForShrinkwrap(yamlFile));

        parsedYaml.packages.Should().HaveCount(2);
        parsedYaml.packages.Should().ContainKey("/query-string/4.3.4");
        parsedYaml.packages.Should().ContainKey("/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2");

        var queryStringPackage = parsedYaml.packages["/query-string/4.3.4"];
        queryStringPackage.dependencies.Should().HaveCount(1);
        queryStringPackage.dependencies.Should().ContainKey("@ms/items-view");
        queryStringPackage.dependencies["@ms/items-view"].Should().BeEquivalentTo("/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2");
        queryStringPackage.dev.Should().BeEquivalentTo("false");

        var itemViewPackage = parsedYaml.packages["/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2"];
        itemViewPackage.dependencies.Should().BeNull();
        itemViewPackage.dev.Should().BeEquivalentTo("true");
    }

    [TestMethod]
    public void CreateDetectedComponentFromPnpmPath()
    {
        var detectedComponent1 = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/@ms/items-view/0.128.9/react-dom@15.6.2+react@15.6.2");
        detectedComponent1.Should().NotBeNull();
        detectedComponent1.Component.Should().NotBeNull();
        ((NpmComponent)detectedComponent1.Component).Name.Should().BeEquivalentTo("@ms/items-view");
        ((NpmComponent)detectedComponent1.Component).Version.Should().BeEquivalentTo("0.128.9");

        var detectedComponent2 = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/@babel/helper-compilation-targets/7.10.4_@babel+core@7.10.5");
        detectedComponent2.Should().NotBeNull();
        detectedComponent2.Component.Should().NotBeNull();
        ((NpmComponent)detectedComponent2.Component).Name.Should().BeEquivalentTo("@babel/helper-compilation-targets");
        ((NpmComponent)detectedComponent2.Component).Version.Should().BeEquivalentTo("7.10.4");

        var detectedComponent3 = PnpmParsingUtilities.CreateDetectedComponentFromPnpmPath("/query-string/4.3.4");
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

    private static IComponentStream CreateComponentStreamForShrinkwrap(string content)
    {
        var packageLockMock = new Mock<IComponentStream>();
        packageLockMock.SetupGet(x => x.Stream).Returns(content.ToStream());
        packageLockMock.SetupGet(x => x.Pattern).Returns("test");
        packageLockMock.SetupGet(x => x.Location).Returns("test");

        return packageLockMock.Object;
    }
}
