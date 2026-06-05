#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Tests;

using System;
using AwesomeAssertions;
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
        var goComponent = new GoComponent("github.com/example/test", "1.2.3", "deadbeef");
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
        packageOne.PackageUrl.ToString().Should().Be("pkg:cocoapods/AFNetworking@4.0.1");
        packageTwo.PackageUrl.ToString().Should().Be("pkg:cocoapods/MapsIndoors@3.24.0");
        packageThree.PackageUrl.ToString().Should().Be("pkg:cocoapods/googleUtilities@7.5.2");
    }

    [TestMethod]
    public void CocoaPodNameShouldPurlWithCustomQualifier()
    {
        // https://github.com/package-url/purl-spec/blob/b8ddd39a6d533b8895f3b741f2e62e2695d82aa4/PURL-TYPES.rst#cocoapods
        var packageOne = new PodComponent("AFNetworking", "4.0.1", "https://custom_repo.example.com/path/to/repo/specs.git");

        packageOne.PackageUrl.ToString().Should().Be("pkg:cocoapods/AFNetworking@4.0.1?repository_url=https:%2F%2Fcustom_repo.example.com%2Fpath%2Fto%2Frepo%2Fspecs.git");
    }

    [TestMethod]
    public void MavenComponentShouldGenerateMavenPurl()
    {
        // https://github.com/package-url/purl-spec/blob/b8ddd39a6d533b8895f3b741f2e62e2695d82aa4/PURL-TYPES.rst#maven
        var component = new MavenComponent("com.google.guava", "guava", "33.0-jre");

        component.PackageUrl.Type.Should().Be("maven");
        component.PackageUrl.Namespace.Should().Be("com.google.guava");
        component.PackageUrl.Name.Should().Be("guava");
        component.PackageUrl.Version.Should().Be("33.0-jre");
        component.PackageUrl.ToString().Should().Be("pkg:maven/com.google.guava/guava@33.0-jre");
    }

    [TestMethod]
    public void GitComponentGithubRepositoryShouldGenerateGithubPurl()
    {
        // https://github.com/package-url/purl-spec/blob/b8ddd39a6d533b8895f3b741f2e62e2695d82aa4/PURL-TYPES.rst#github
        var component = new GitComponent(new Uri("https://github.com/google/guava"), "abcdef1234567890");

        component.PackageUrl.Type.Should().Be("github");
        component.PackageUrl.Namespace.Should().Be("google");
        component.PackageUrl.Name.Should().Be("guava");
        component.PackageUrl.Version.Should().Be("abcdef1234567890");
        component.PackageUrl.ToString().Should().Be("pkg:github/google/guava@abcdef1234567890");
    }

    [TestMethod]
    public void GitComponentGithubRepositoryWithDotGitSuffixShouldStripIt()
    {
        var component = new GitComponent(new Uri("https://github.com/google/guava.git"), "abcdef1234567890");

        component.PackageUrl.Name.Should().Be("guava", "the .git suffix is not part of the canonical repo name");
        component.PackageUrl.ToString().Should().Be("pkg:github/google/guava@abcdef1234567890");
    }

    [TestMethod]
    public void GitComponentGithubRepositoryWithTrailingSlashShouldBeNormalized()
    {
        var component = new GitComponent(new Uri("https://github.com/google/guava/"), "abcdef1234567890");

        component.PackageUrl.ToString().Should().Be("pkg:github/google/guava@abcdef1234567890");
    }

    [TestMethod]
    public void GitComponentGithubHostMatchIsCaseInsensitive()
    {
        var component = new GitComponent(new Uri("https://GitHub.com/google/guava"), "abcdef1234567890");

        component.PackageUrl.ToString().Should().Be("pkg:github/google/guava@abcdef1234567890");
    }

    [TestMethod]
    public void GitComponentNonGithubRepositoryShouldHaveNoPackageUrl()
    {
        // GitLab / Bitbucket / Azure DevOps / GitHub Enterprise have no canonical PURL representation today.
        // Consumers should fall back to RepositoryUrl in this case.
        var gitlab = new GitComponent(new Uri("https://gitlab.com/foo/bar"), "abcdef1234567890");
        var bitbucket = new GitComponent(new Uri("https://bitbucket.org/foo/bar"), "abcdef1234567890");
        var ado = new GitComponent(new Uri("https://dev.azure.com/org/proj/_git/repo"), "abcdef1234567890");
        var ghEnterprise = new GitComponent(new Uri("https://github.contoso.com/foo/bar"), "abcdef1234567890");

        gitlab.PackageUrl.Should().BeNull();
        bitbucket.PackageUrl.Should().BeNull();
        ado.PackageUrl.Should().BeNull();
        ghEnterprise.PackageUrl.Should().BeNull();
    }

    [TestMethod]
    public void GitComponentMalformedGithubUrlShouldHaveNoPackageUrl()
    {
        // Owner only, or paths deeper than owner/repo (e.g. browse URLs) — not canonical repository URLs.
        var ownerOnly = new GitComponent(new Uri("https://github.com/google"), "abcdef1234567890");
        var tooDeep = new GitComponent(new Uri("https://github.com/google/guava/tree/main"), "abcdef1234567890");
        var rootOnly = new GitComponent(new Uri("https://github.com/"), "abcdef1234567890");

        ownerOnly.PackageUrl.Should().BeNull();
        tooDeep.PackageUrl.Should().BeNull();
        rootOnly.PackageUrl.Should().BeNull();
    }

    [TestMethod]
    public void GitComponentMissingCommitHashShouldHaveNoPackageUrl()
    {
        // CommitHash is required via the public ctor, but the parameterless deserialization ctor allows null.
        var component = new GitComponent
        {
            RepositoryUrl = new Uri("https://github.com/google/guava"),
        };

        component.PackageUrl.Should().BeNull();
    }
}
