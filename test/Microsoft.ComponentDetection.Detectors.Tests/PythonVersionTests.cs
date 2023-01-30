namespace Microsoft.ComponentDetection.Detectors.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PythonVersionTests
{
    [TestMethod]
    public void TestBasicVersionConstruction()
    {
        var pythonVersion = new PythonVersion("4!3.2.1.1rc2.post99.dev2");

        Assert.AreEqual(4, pythonVersion.Epoch);
        Assert.AreEqual("3.2.1.1", pythonVersion.Release);
        Assert.AreEqual("rc", pythonVersion.PreReleaseLabel);
        Assert.AreEqual(99, pythonVersion.PostNumber);
        Assert.AreEqual("dev", pythonVersion.DevLabel);
        Assert.AreEqual(2, pythonVersion.DevNumber);
    }

    [TestMethod]
    public void TestDefaultDevVersionConstruction()
    {
        var pythonVersion = new PythonVersion("4!3.2.1.1rc2.post90.dev");

        Assert.AreEqual(4, pythonVersion.Epoch);
        Assert.AreEqual("3.2.1.1", pythonVersion.Release);
        Assert.AreEqual("rc", pythonVersion.PreReleaseLabel);
        Assert.AreEqual(2, pythonVersion.PreReleaseNumber);
        Assert.AreEqual(90, pythonVersion.PostNumber);
        Assert.AreEqual("dev", pythonVersion.DevLabel);
        Assert.AreEqual(0, pythonVersion.DevNumber);
    }

    [TestMethod]
    public void TestPythonVersionComplexComparisons()
    {
        // This is a list of versions supplied by PEP440 for testing + a few other valid versions missing in their examples (namely "v" prefix and local version using "+")
        var versionItems = new List<string>
        {
            "1.0.dev",
            "1.0.dev456",
            "1.0a1",
            "1.0a2.dev456",
            "1.0a12.dev456",
            "1.0a12",
            "1.0b1.dev456",
            "1.0b2",
            "1.0b2.post345.dev456",
            "1.0b2.post345",
            "1.0rc1.dev",
            "1.0rc1.dev456",
            "1.0rc1",
            "1.0",
            "1.0.post456.dev34",
            "1.0.post456",
            "1.1.dev",
            "1.1.dev1",
            "1.1",
            "1.1.1.dev17+gcae73d8.d20200403",
            "1.1.1.dev18",
            "1.1.1",
            "2.10.0.dev1",
            "v2.10.0.dev2",
            "v2.10.0",
        }.Select(x => new { Raw = x, Version = new PythonVersion(x) }).ToList();

        for (var i = 1; i < versionItems.Count; i++)
        {
            var versionItem = versionItems[i];
            versionItem.Version.Valid.Should().BeTrue($"Version should be correctly parsed. Version={versionItem.Raw}");
            versionItem.Version.Should().BeGreaterThan(versionItems[i - 1].Version);
        }
    }

    [TestMethod]
    public void TestVersionValidForSpec()
    {
        IList<(IList<string>, IList<string>, IList<string>)> testCases = new List<(IList<string>, IList<string>, IList<string>)>
        {
            (new List<string> { "==1.0" }, new List<string> { "1.0" }, new List<string> { "1.0.1", "2.0", "0.1" }),
            (new List<string> { "==1.4.*" }, new List<string> { "1.4", "1.4.1", "1.4.2", "1.4.3" }, new List<string> { "1.0.1", "2.0", "0.1", "1.5", "1.5.0" }),
            (new List<string> { ">=1.0" }, new List<string> { "1.0", "1.1", "1.5" }, new List<string> { "0.9" }),
            (new List<string> { ">=1.0", "<=1.4" }, new List<string> { "1.0", "1.1", "1.4" }, new List<string> { "0.9", "1.5" }),
            (new List<string> { ">1.0", "<1.4" }, new List<string> { "1.1", "1.3" }, new List<string> { "0.9", "1.5", "1.0", "1.4" }),
            (new List<string> { ">1.0", "<1.4", "!=1.2" }, new List<string> { "1.1", "1.3" }, new List<string> { "0.9", "1.5", "1.0", "1.4", "1.2" }),
            (new List<string> { "==1.1.1.dev17+gcae73d8.d20200403" }, new List<string> { "1.1.1.dev17", "v1.1.1.dev17", "1.1.1.dev17+gcae73d8.d20200403" }, new List<string> { "1.1.1.dev18", "1.0.1", "1.1.1" }),
        };

        foreach (var (specs, validVersions, invalidVersions) in testCases)
        {
            Assert.IsTrue(validVersions.All(x => PythonVersionUtilities.VersionValidForSpec(x, specs)));
            Assert.IsTrue(invalidVersions.All(x => !PythonVersionUtilities.VersionValidForSpec(x, specs)));
        }
    }

    [TestMethod]
    public void TestVersionValidForSpec_VersionIsNotValid_ArgumentExceptionIsThrown()
    {
        Action action = () => PythonVersionUtilities.VersionValidForSpec("notvalid", new List<string> { "==1.0" });
        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void TestVersionValidForSpec_SomeSpecIsNotValid_ArgumentExceptionIsThrown()
    {
        Action action = () => PythonVersionUtilities.VersionValidForSpec("1.0.0", new List<string> { "==notvalid" });
        action.Should().Throw<ArgumentException>();

        action = () => PythonVersionUtilities.VersionValidForSpec("1.0.0", new List<string> { "==1.1+gcae73d8.d20200403+1.0" });
        action.Should().Throw<ArgumentException>();
    }
}
