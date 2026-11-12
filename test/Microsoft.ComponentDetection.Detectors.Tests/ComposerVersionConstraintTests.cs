namespace Microsoft.ComponentDetection.Detectors.Tests;

using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.Composer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ComposerVersionConstraintTests
{
    [TestMethod]
    [DataRow("1.2.3", "1.2.3")]
    [DataRow(" v1.2.3 ", "v1.2.3")]
    [DataRow("= 1.2.3", "1.2.3")]
    [DataRow("==1.2.3-beta2", "1.2.3-beta2")]
    [DataRow("1.2", "1.2")]
    [DataRow("1.2.3.4", "1.2.3.4")]
    public void ExactManifestVersion_PreservesPinnedIdentity(string constraint, string expected)
    {
        ComposerVersionConstraint.TryGetExactVersion(constraint, out var version).Should().BeTrue();
        version.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("^1.0", "1.2.3", true)]
    [DataRow("^1.0", "^2.0", false)]
    [DataRow("^2.0", "1.0", false)]
    [DataRow("~1.2", "1.9.0", true)]
    [DataRow("~1.2", "2.0.0", false)]
    [DataRow("~1.2.3", "1.3.0", false)]
    [DataRow("1.2", "1.2.0", true)]
    [DataRow("1.2", "1.2.1", false)]
    [DataRow("1.*", "1.9.0", true)]
    [DataRow(">=1.0, <2.0", "1.4.0", true)]
    [DataRow(">= 1.0 < 2.0", "1.4.0", true)]
    [DataRow("^1 | ^3", "3.1.0", true)]
    [DataRow("^1 || ^3", "2.1.0", false)]
    [DataRow("^1||^3", "3.1.0", true)]
    [DataRow("^1|^3", "2.1.0", false)]
    [DataRow("^1\t||\r\n^3", "3.1.0", true)]
    [DataRow("^1\u00a0|\u2003^3", "3.1.0", true)]
    [DataRow("^2.0@dev", "2.x-dev", true)]
    [DataRow("dev-main as 2.x-dev", "^2", false)]
    [DataRow("dev-main#123", "dev-main", true)]
    [DataRow("dev-main", "dev-next", false)]
    [DataRow("*", "dev-main", true)]
    [DataRow("dev-main", "*", true)]
    [DataRow("v1.2.3", "1.2.3", true)]
    [DataRow("nonsense", "*", false)]
    [DataRow("nonsense", "nonsense", false)]
    [DataRow("~9999999999999999999999.1", "1.0", false)]
    [DataRow("<=1.2", "1.2.1", false)]
    [DataRow(">1.2", "1.2.1", true)]
    [DataRow(">=1.2 <2", "1.2.0-beta2", true)]
    [DataRow("!=1.2.3", "1.2.3", false)]
    [DataRow("!=1.2.3", "1.2.4", true)]
    [DataRow("^1 !=1.2.3", "1.2.3", false)]
    [DataRow("^1 !=1.2.3", "1.2.4", true)]
    [DataRow(">=1 <2 !=1.2.3", ">=1.2.3-stable <=1.2.3", false)]
    [DataRow(">=1 <2 !=1.2.3", ">=1.2.3 <=1.2.3", true)]
    [DataRow("^1 !=1.2.3 || ^3", "3.0.0", true)]
    [DataRow("1.2.3.4", "1.2.3.4", true)]
    [DataRow("1.2.3.4", "1.2.3.5", false)]
    [DataRow("~1.2.3.4", "1.2.3.9", true)]
    [DataRow("~1.2.3.4", "1.2.4", false)]
    [DataRow("^0.0.3.4", "0.0.3.9", true)]
    [DataRow("^0.0.3.4", "0.0.4", false)]
    [DataRow("1.0 - 2.0", "2.0.9", true)]
    [DataRow("1.0 - 2.0", "2.1.0", false)]
    [DataRow("1.0.0 - 2.0.0", "2.0.1", false)]
    [DataRow("1.2.3beta2", "1.2.3-beta.2", true)]
    [DataRow(">1.2.3-RC1 <1.2.3-stable", "1.2.3-rc2", true)]
    [DataRow(">1.2.3-RC1 <1.2.3", "1.2.3-rc2", false)]
    [DataRow(">1.2.3", "1.2.3-p1", true)]
    [DataRow("<0.0.0-dev", "*", false)]
    [DataRow("!=dev-main", "dev-main", false)]
    [DataRow("!=dev-main", "dev-next", true)]
    [DataRow("!=dev-main", "1.0", true)]
    [DataRow("!=1.0", "dev-main", true)]
    [DataRow("1.2.x-dev", "1.2.9", true)]
    [DataRow("1.2.x-dev", "1.3.0", false)]
    [DataRow("", "*", false)]
    [DataRow("~9223372036854775807", "1.0", false)]
    public void VirtualCompatibility_RespectsComposerConstraintSemantics(string required, string provided, bool expected) =>
        ComposerVersionConstraint.Intersects(required, provided).Should().Be(expected);

    [TestMethod]
    [DataRow("dev-main as 1.0.0", "dev-main", true)]
    [DataRow("dev-main as 1.0.0", "1.0.0", false)]
    [DataRow("dev-main as 1.0.0", "dev-other", false)]
    [DataRow(" dev-main   as   1.0.0 ", "dev-main", true)]
    [DataRow("dev-main#reference as 1.0.0", "dev-main", true)]
    [DataRow("1.0.0 as 2.0.0", "1.0.0", true)]
    [DataRow("1.0.0 as 2.0.0", "2.0.0", false)]
    [DataRow("dev-main as 1.0.0 || ^2", "2.1.0", true)]
    [DataRow("^2 || dev-main as 1.0.0", "2.1.0", true)]
    [DataRow("^2 || dev-main as 1.0.0", "1.0.0", false)]
    [DataRow("dev-main as 1.0.0, !=dev-other", "dev-main", true)]
    [DataRow("dev-main as 1.0.0, !=dev-main", "dev-main", false)]
    public void InlineAliases_CompareNativeSourceConstraints(string required, string provided, bool expected)
    {
        ComposerVersionConstraint.TryIntersects(required, provided, out var intersects).Should().BeTrue();
        intersects.Should().Be(expected);
        ComposerVersionConstraint.TryIntersects(provided, required, out var reverse).Should().BeTrue();
        reverse.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("1.2.3", "1.2.3.0", true)]
    [DataRow(" v1.2.3 ", "1.2.3.0", true)]
    [DataRow(" dev-main ", "dev-main", true)]
    [DataRow(" release-next#revision ", "release-next#revision", true)]
    [DataRow("v1.2.3-beta2", "1.2.3.0-beta.2", true)]
    [DataRow("1.2.3", "1.2.3.1", false)]
    [DataRow("dev-main", "dev-next", false)]
    [DataRow("1.x-dev", "1.9999999.9999999.9999999-dev", true)]
    [DataRow("1.2.x-dev", "1.2.9999999.9999999-dev", true)]
    public void LockAliasVersionMatching_UsesComposerNumericNormalization(string left, string right, bool expected) =>
        ComposerVersionConstraint.VersionsEqual(left, right).Should().Be(expected);

    [TestMethod]
    [DataRow("|^1")]
    [DataRow("^1|")]
    [DataRow("||^1")]
    [DataRow("^1||")]
    [DataRow("^1|||^3")]
    [DataRow("^1||||^3")]
    [DataRow("^1| |^3")]
    public void EmptyAlternatives_AreRejected(string constraint) =>
        ComposerVersionConstraint.TryIntersects(constraint, "*", out _).Should().BeFalse();

    [TestMethod]
    public void UnsupportedConstraint_IsDistinctFromDisjointRanges()
    {
        ComposerVersionConstraint.TryIntersects("^1", "^2", out var disjoint).Should().BeTrue();
        disjoint.Should().BeFalse();
        ComposerVersionConstraint.TryIntersects("^1", "unsupported", out _).Should().BeFalse();
        ComposerVersionConstraint.TryIntersects("unsupported as 1.0.0", "1.0.0", out _).Should().BeFalse();
    }
}
