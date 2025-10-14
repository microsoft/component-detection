#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class PipReportUtilitiesTests
{
    [TestInitialize]
    public void TestInitialize()
    {
    }

    [TestMethod]
    public void NormalizePackageName_ExpectedEquivalent()
    {
        // Example test cases from https://packaging.python.org/en/latest/specifications/name-normalization/#name-normalization
        const string normalizedForm = "friendly-bard";

        PipReportUtilities.NormalizePackageNameFormat("friendly-bard").Should().Be(normalizedForm);
        PipReportUtilities.NormalizePackageNameFormat("Friendly-Bard").Should().Be(normalizedForm);
        PipReportUtilities.NormalizePackageNameFormat("FRIENDLY-BARD").Should().Be(normalizedForm);
        PipReportUtilities.NormalizePackageNameFormat("friendly.bard").Should().Be(normalizedForm);
        PipReportUtilities.NormalizePackageNameFormat("friendly_bard").Should().Be(normalizedForm);
        PipReportUtilities.NormalizePackageNameFormat("friendly--bard").Should().Be(normalizedForm);
        PipReportUtilities.NormalizePackageNameFormat("FrIeNdLy-._.-bArD").Should().Be(normalizedForm);
    }

    [TestMethod]
    [DataRow("1.0.0")]
    [DataRow("2012.10")]
    [DataRow("0.1rc2.post3")]
    [DataRow("1.0.post2.dev3")]
    [DataRow("1!2.0")]
    [DataRow("1.0.post1")]
    public void IsCanonicalVersion_ReturnsTrue_WhenVersionMatchesCanonicalVersionPattern(string version)
    {
        // Act
        var isCanonicalVersion = PipReportUtilities.IsCanonicalVersion(version);

        // Assert
        isCanonicalVersion.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("0.0.1-beta")]
    [DataRow("1.0.0-alpha.1")]
    [DataRow(".1")]
    [DataRow("-pkg-version-")]
    public void IsCanonicalVersion_ReturnsFalse_WhenVersionDoesNotMatchCanonicalVersionPattern(string version)
    {
        // Act
        var isCanonicalVersion = PipReportUtilities.IsCanonicalVersion(version);

        // Assert
        isCanonicalVersion.Should().BeFalse();
    }
}
