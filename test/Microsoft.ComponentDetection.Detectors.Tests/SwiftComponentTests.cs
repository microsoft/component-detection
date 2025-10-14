#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.Swift;

using System;
using System.Collections.Generic;
using FluentAssertions;
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
        var packageUrl = "https://github.com/Alamofire/Alamofire";
        var hash = "f455c2975872ccd2d9c81594c658af65716e9b9a";

        var component = new SwiftComponent(name, version, packageUrl, hash);

        component.Name.Should().Be(name);
        component.Version.Should().Be(version);
        component.Type.Should().Be(ComponentType.Swift);
        component.Id.Should().Be($"{name} {version} - {component.Type}");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenNameIsNull()
    {
        Action action = () => new SwiftComponent(null, "5.9.1", "https://github.com/Alamofire/Alamofire", "f455c2975872ccd2d9c81594c658af65716e9b9a");
        action.Should().Throw<ArgumentException>().WithMessage("*name*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenVersionIsNull()
    {
        Action action = () => new SwiftComponent("alamofire", null, "https://github.com/Alamofire/Alamofire", "f455c2975872ccd2d9c81594c658af65716e9b9a");
        action.Should().Throw<ArgumentException>().WithMessage("*version*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenPackageUrlIsNull()
    {
        Action action = () => new SwiftComponent("alamofire", "5.9.1", null, "f455c2975872ccd2d9c81594c658af65716e9b9a");
        action.Should().Throw<ArgumentException>().WithMessage("*packageUrl*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenHashIsNull()
    {
        Action action = () => new SwiftComponent("alamofire", "5.9.1", "https://github.com/Alamofire/Alamofire", null);
        action.Should().Throw<ArgumentException>().WithMessage("*hash*");
    }

    [TestMethod]
    public void Constructor_ShouldThrowException_WhenPackageUrlIsInvalid()
    {
        Action action = () => new SwiftComponent("alamofire", "5.9.1", "invalid-url", "f455c2975872ccd2d9c81594c658af65716e9b9a");
        action.Should().Throw<UriFormatException>();
    }

    [TestMethod]
    public void PackageURL_ShouldReturnCorrectPackageURL_GithubHostname()
    {
        var name = "alamofire";
        var version = "5.9.1";
        var packageUrl = "https://github.com/Alamofire/Alamofire";
        var hash = "f455c2975872ccd2d9c81594c658af65716e9b9a";

        var component = new SwiftComponent(name, version, packageUrl, hash);

        var expectedPackageURL = new PackageURL(
            type: "swift",
            @namespace: "github.com/Alamofire",
            name: name,
            version: version,
            qualifiers: new SortedDictionary<string, string>
            {
                { "repository_url", packageUrl },
            },
            subpath: null);

        component.PackageURL.Should().BeEquivalentTo(expectedPackageURL);
    }

    [TestMethod]
    public void PackageURL_ShouldReturnCorrectPackageURL_GithubHostname_Alternate()
    {
        var name = "alamofire";
        var version = "5.9.1";
        var packageUrl = "https://giTHub.com/Alamofire/Alamofire";
        var hash = "f455c2975872ccd2d9c81594c658af65716e9b9a";

        var component = new SwiftComponent(name, version, packageUrl, hash);

        var expectedPackageURL = new PackageURL(
            type: "swift",
            @namespace: "github.com/Alamofire",
            name: name,
            version: version,
            qualifiers: new SortedDictionary<string, string>
            {
                { "repository_url", "https://github.com/Alamofire/Alamofire" },
            },
            subpath: null);

        component.PackageURL.Should().BeEquivalentTo(expectedPackageURL);
    }

    [TestMethod]
    public void PackageURL_ShouldReturnCorrectPackageURL_OtherHostname()
    {
        var name = "alamofire";
        var version = "5.9.1";
        var packageUrl = "https://otherhostname.com/Alamofire/Alamofire";
        var hash = "f455c2975872ccd2d9c81594c658af65716e9b9a";

        var component = new SwiftComponent(name, version, packageUrl, hash);

        var expectedPackageURL = new PackageURL(
            type: "swift",
            @namespace: "otherhostname.com",
            name: name,
            version: version,
            qualifiers: new SortedDictionary<string, string>
            {
                { "repository_url", packageUrl },
            },
            subpath: null);

        component.PackageURL.Should().BeEquivalentTo(expectedPackageURL);
    }
}
