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

        var component = new SwiftComponent
        {
            Name = name,
            Version = version,
            RepositoryUrl = new Uri(repositoryUrl),
            CommitHash = commitHash,
            Kind = kind,
        };

        component.Name.Should().Be(name);
        component.Version.Should().Be(version);
        component.Kind.Should().Be(kind);
        component.CommitHash.Should().Be(commitHash);
        component.RepositoryUrl.Should().Be(new Uri(repositoryUrl));
        component.Type.Should().Be(ComponentType.Swift);
        component.Id.Should().Be(
            $"{repositoryUrl} {version} - {component.Type}");
    }

    [TestMethod]
    public void Id_ShouldDistinguishRepositories()
    {
        var component = new SwiftComponent
        {
            Name = "alamofire",
            Version = "5.9.1",
            RepositoryUrl = new Uri("https://github.com/Alamofire/Alamofire"),
            CommitHash = "f455c2975872ccd2d9c81594c658af65716e9b9a",
            Kind = "remoteSourceControl",
        };
        var differentRepository = new SwiftComponent
        {
            Name = "alamofire",
            Version = "5.9.1",
            RepositoryUrl = new Uri("https://github.com/example/Alamofire"),
            CommitHash = "f455c2975872ccd2d9c81594c658af65716e9b9a",
            Kind = "remoteSourceControl",
        };

        component.Id.Should().NotBe(differentRepository.Id);
    }

    [TestMethod]
    public void Serialization_ShouldIncludeCommitHash()
    {
        var commitHash = "f455c2975872ccd2d9c81594c658af65716e9b9a";
        TypedComponent component = new SwiftComponent
        {
            Name = "alamofire",
            Version = "5.9.1",
            RepositoryUrl = new Uri("https://github.com/Alamofire/Alamofire"),
            CommitHash = commitHash,
            Kind = "remoteSourceControl",
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(component));

        json.RootElement.GetProperty("kind").GetString().Should().Be("remoteSourceControl");
        json.RootElement.GetProperty("commitHash").GetString().Should().Be(commitHash);
    }

    [TestMethod]
    public void PackageURL_ShouldReturnCorrectPackageURL_GithubHostname()
    {
        var name = "alamofire";
        var version = "5.9.1";
        var repositoryUrl = "https://github.com/Alamofire/Alamofire";
        var hash = "f455c2975872ccd2d9c81594c658af65716e9b9a";

        var component = new SwiftComponent
        {
            Name = name,
            Version = version,
            RepositoryUrl = new Uri(repositoryUrl),
            CommitHash = hash,
            Kind = "remoteSourceControl",
        };

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

        var component = new SwiftComponent
        {
            Name = name,
            Version = version,
            RepositoryUrl = new Uri(repositoryUrl),
            CommitHash = hash,
            Kind = "remoteSourceControl",
        };

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

        var component = new SwiftComponent
        {
            Name = name,
            Version = version,
            RepositoryUrl = new Uri(repositoryUrl),
            CommitHash = hash,
            Kind = "remoteSourceControl",
        };

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
