#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Tests;

using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PurlGenerationTests
{
    [TestMethod]
    public void NpmPackageNameShouldBeLowerCase()
    {
        // According to the spec package name should not have uppercase letters
        // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L656
        var npmComponent = new NpmComponent("TEST", "1.2.3");
        npmComponent.PackageUrl.Name.Should().Be("test");
    }

    [TestMethod]
    public void GoPackageShouldPreferHashOverVersion()
    {
        // Commit should be used in place of version when available
        // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L610
        var goComponent = new GoComponent("test", "1.2.3", "deadbeef");
        goComponent.PackageUrl.Version.Should().Be("deadbeef");
    }

    [TestMethod]
    public void PipPackageShouldBeModified()
    {
        // Package name should be lowercased and replace '_' with '-'
        // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L680
        var pipComponent = new PipComponent("CHANGE_ME", "1.2.3");
        pipComponent.PackageUrl.Name.Should().Be("change-me");
    }

    [TestMethod]
    public void DebianAndUbuntuAreDebType()
    {
        // Ubuntu and debian are "deb" component types
        // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L537
        var ubuntuComponent = new LinuxComponent("Ubuntu", "18.04", "bash", "1");
        var debianComponent = new LinuxComponent("Debian", "buster", "bash", "1");

        ubuntuComponent.PackageUrl.Type.Should().Be("deb");
        debianComponent.PackageUrl.Type.Should().Be("deb");
    }

    [TestMethod]
    public void CentOsFedoraAndRHELAreRpmType()
    {
        // CentOS, Fedora and RHEL use "rpm" component types
        // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L693
        var centosComponent = new LinuxComponent("CentOS", "18.04", "bash", "1");
        var fedoraComponent = new LinuxComponent("Fedora", "18.04", "bash", "1");
        var rhelComponent = new LinuxComponent("Red Hat Enterprise Linux", "18.04", "bash", "1");

        centosComponent.PackageUrl.Type.Should().Be("rpm");
        fedoraComponent.PackageUrl.Type.Should().Be("rpm");
        rhelComponent.PackageUrl.Type.Should().Be("rpm");
    }

    [TestMethod]
    public void AlpineAndUnknownDoNotHavePurls()
    {
        // Alpine is not yet defined
        // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L711
        var alpineComponent = new LinuxComponent("Alpine", "3.13", "bash", "1");
        var unknownLinuxComponent = new LinuxComponent("Linux", "0", "bash", "1'");

        alpineComponent.PackageUrl.Should().BeNull();
        unknownLinuxComponent.PackageUrl.Should().BeNull();
    }

    [TestMethod]
    public void DistroNamesAreLowerCased()
    {
        // Distros must be lower cased for both deb and rpm
        // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L537
        // https://github.com/package-url/purl-spec/blame/180c46d266c45aa2bd81a2038af3f78e87bb4a25/README.rst#L694
        var ubuntuComponent = new LinuxComponent("UbUnTu", "18.04", "bash", "1");
        var fedoraComponent = new LinuxComponent("FeDoRa", "22", "bash", "1");

        ubuntuComponent.PackageUrl.Namespace.Should().Be("ubuntu");
        fedoraComponent.PackageUrl.Namespace.Should().Be("fedora");
    }

    [TestMethod]
    public void CocoaPodNameShouldSupportPurl()
    {
        // https://github.com/package-url/purl-spec/blob/b8ddd39a6d533b8895f3b741f2e62e2695d82aa4/PURL-TYPES.rst#cocoapods
        var packageOne = new PodComponent("AFNetworking", "4.0.1");
        var packageTwo = new PodComponent("MapsIndoors", "3.24.0");
        var packageThree = new PodComponent("googleUtilities", "7.5.2");

        packageOne.PackageUrl.Type.Should().Be("cocoapods");
        packageOne.PackageUrl.ToString().Should().Be("pkg:cocoapods/afnetworking@4.0.1");
        packageTwo.PackageUrl.ToString().Should().Be("pkg:cocoapods/mapsindoors@3.24.0");
        packageThree.PackageUrl.ToString().Should().Be("pkg:cocoapods/googleutilities@7.5.2");
    }

    [TestMethod]
    public void CocoaPodNameShouldPurlWithCustomQualifier()
    {
        // https://github.com/package-url/purl-spec/blob/b8ddd39a6d533b8895f3b741f2e62e2695d82aa4/PURL-TYPES.rst#cocoapods
        var packageOne = new PodComponent("AFNetworking", "4.0.1", "https://custom_repo.example.com/path/to/repo/specs.git");

        packageOne.PackageUrl.ToString().Should().Be("pkg:cocoapods/afnetworking@4.0.1?repository_url=https://custom_repo.example.com/path/to/repo/specs.git");
    }
}
