#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.Swift;

using System;
using System.Collections.Generic;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PackageUrl;

[TestClass]
public class SwiftComponentTests
{
    [TestMethod]
    public void Constructor_ShouldInitializeProperties()
    {
        var name = "alamofire";
        var version = "5.9.1";
        var repositoryUrl = "https://github.com/Alamofire/Alamofire";
        var kind = "remoteSourceControl";
        var commitHash = "f455c2975872ccd2d9c81594c658af65716e9b9a";

        var component = new SwiftComponent(name, version, repositoryUrl, commitHash, kind);

        component.Name.Should().Be(name);
        component.Version.Should().Be(version);
        component.Kind.Should().Be(kind);
        component.CommitHash.Should().Be(commitHash);
        component.RepositoryUrl.Should().Be(new Uri(repositoryUrl));
        component.Type.Should().Be(ComponentType.Swift);
        component.Id.Should().Be(
            $"{repositoryUrl} {commitHash} - {component.Type}");
    }

    [TestMethod]
    public void Id_ShouldDistinguishRepositoriesAndCommitHashes()
    {
        var component = new SwiftComponent(
            "alamofire",
            "5.9.1",
            "https://github.com/Alamofire/Alamofire",
            "f455c2975872ccd2d9c81594c658af65716e9b9a",
            "remoteSourceControl");
        var differentRepository = new SwiftComponent(
            "alamofire",
            "5.9.1",
            "https://github.com/example/Alamofire",
            "f455c2975872ccd2d9c81594c658af65716e9b9a",
            "remoteSourceControl");
        var differentCommit = new SwiftComponent(
            "alamofire",
            "5.9.1",
            "https://github.com/Alamofire/Alamofire",
            "63dfa86548c4e5d5c6fd6ed42f638e388cbce529",
            "remoteSourceControl");

        component.Id.Should().NotBe(differentRepository.Id);
        component.Id.Should().NotBe(differentCommit.Id);
    }

    [TestMethod]
    public void Serialization_ShouldIncludeCommitHash()
    {
        var commitHash = "f455c2975872ccd2d9c81594c658af65716e9b9a";
        TypedComponent component = new SwiftComponent(
            name: "alamofire",
            version: "5.9.1",
            repositoryUrl: "https://github.com/Alamofire/Alamofire",
            hash: commitHash,
            kind: "remoteSourceControl");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(component));

        json.RootElement.GetProperty("kind").GetString().Should().Be("remoteSourceControl");
        json.RootElement.GetProperty("commitHash").GetString().Should().Be(commitHash);
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenKindIsNull()
    {
        Action action = () =>
            new SwiftComponent(
                "alamofire",
                "5.9.1",
                "https://github.com/Alamofire/Alamofire",
                "f455c2975872ccd2d9c81594c658af65716e9b9a",
                null
            );
        action.Should().Throw<ArgumentException>().WithMessage("*kind*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenNameIsNull()
    {
        Action action = () =>
            new SwiftComponent(
                null,
                "5.9.1",
                "https://github.com/Alamofire/Alamofire",
                "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "remoteSourceControl"
            );
        action.Should().Throw<ArgumentException>().WithMessage("*name*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenVersionIsNull()
    {
        Action action = () =>
            new SwiftComponent(
                "alamofire",
                null,
                "https://github.com/Alamofire/Alamofire",
                "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "remoteSourceControl"
            );
        action.Should().Throw<ArgumentException>().WithMessage("*version*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenRepositoryUrlIsNull()
    {
        Action action = () =>
            new SwiftComponent(
                "alamofire",
                "5.9.1",
                null,
                "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "remoteSourceControl"
            );
        action.Should().Throw<ArgumentException>().WithMessage("*repositoryUrl*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenHashIsNull()
    {
        Action action = () =>
            new SwiftComponent(
                "alamofire",
                "5.9.1",
                "https://github.com/Alamofire/Alamofire",
                null,
                "remoteSourceControl"
            );
        action.Should().Throw<ArgumentException>().WithMessage("*hash*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenRepositoryUrlIsInvalid()
    {
        Action action = () =>
            new SwiftComponent(
                "alamofire",
                "5.9.1",
                "invalid-url",
                "f455c2975872ccd2d9c81594c658af65716e9b9a",
                "remoteSourceControl"
            );
        action.Should().Throw<UriFormatException>();
    }

    [TestMethod]
    public void PackageURL_ShouldReturnCorrectPackageURL_GithubHostname()
    {
        var name = "alamofire";
        var version = "5.9.1";
        var repositoryUrl = "https://github.com/Alamofire/Alamofire";
        var hash = "f455c2975872ccd2d9c81594c658af65716e9b9a";

        var component = new SwiftComponent(name, version, repositoryUrl, hash, "remoteSourceControl");

        var expectedPackageURL = new PackageURL(
            type: "swift",
            @namespace: "github.com/Alamofire",
            name: name,
            version: version,
            qualifiers: new SortedDictionary<string, string> { { "repository_url", repositoryUrl } },
            subpath: null
        );

        component.PackageUrl.Should().BeEquivalentTo(expectedPackageURL);
    }

    [TestMethod]
    public void PackageURL_ShouldReturnCorrectPackageURL_GithubHostname_Alternate()
    {
        var name = "alamofire";
        var version = "5.9.1";
        var repositoryUrl = "https://giTHub.com/Alamofire/Alamofire";
        var hash = "f455c2975872ccd2d9c81594c658af65716e9b9a";

        var component = new SwiftComponent(name, version, repositoryUrl, hash, "remoteSourceControl");

        var expectedPackageURL = new PackageURL(
            type: "swift",
            @namespace: "github.com/Alamofire",
            name: name,
            version: version,
            qualifiers: new SortedDictionary<string, string>
            {
                { "repository_url", "https://github.com/Alamofire/Alamofire" },
            },
            subpath: null
        );

        component.PackageUrl.Should().BeEquivalentTo(expectedPackageURL);
    }

    [TestMethod]
    public void PackageURL_ShouldReturnCorrectPackageURL_OtherHostname()
    {
        var name = "alamofire";
        var version = "5.9.1";
        var repositoryUrl = "https://otherhostname.com/Alamofire/Alamofire";
        var hash = "f455c2975872ccd2d9c81594c658af65716e9b9a";

        var component = new SwiftComponent(name, version, repositoryUrl, hash, "remoteSourceControl");

        var expectedPackageURL = new PackageURL(
            type: "swift",
            @namespace: "otherhostname.com",
            name: name,
            version: version,
            qualifiers: new SortedDictionary<string, string> { { "repository_url", repositoryUrl } },
            subpath: null
        );

        component.PackageUrl.Should().BeEquivalentTo(expectedPackageURL);
    }
}
