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
}
