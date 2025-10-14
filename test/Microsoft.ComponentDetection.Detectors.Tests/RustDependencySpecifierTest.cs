#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class RustDependencySpecifierTest
{
    [TestMethod]
    public void DoesntMatch_WhenNoDependencyAdded()
    {
        var testCases = new List<(bool ShouldMatch, string CaseName, string SpecifierName, string SpecifierRange)>
        {
            (false, "DoesntMatch_WhenNoDependencyAdded", null, null),
            (false, "DoesntMatch_WhenDependencyAddedForDifferentPackage", "some-other-package", "1.2.3"),
            (false, "DoesntMatch_WhenPointVersionDoesntMatch", "some-cargo-package", "1.2.4"),
            (true, "Matches_WhenPointVersionMatches", "some-cargo-package", "1.2.3"),
            (true, "Matches_WhenRangeMatches_BottomInclusive", "some-cargo-package", ">= 1.2.3, < 1.3.3"),
            (true, "Matches_WhenRangeMatches_TopInclusive", "some-cargo-package", ">= 0.1.1, <= 1.2.3"),
            (false, "DoesntMatch_WhenRangeExcludes_TopExclusive", "some-cargo-package", ">= 0.1.1, < 1.2.3"),
            (false, "DoesntMatch_WhenRangeExcludes_BottomExclusive", "some-cargo-package", "> 1.2.3, < 1.2.5"),
            (true, "Matches_WhenRangeIncludes_SingleTerm", "some-cargo-package", "> 1.2.2"),
            (false, "DoesntMatch_WhenRangeExcludes_SingleTerm", "some-cargo-package", "> 1.2.3"),
            (true, "Matches_~", "some-cargo-package", "~1.2.2"),
            (false, "DoesntMatch_~", "some-cargo-package", "~1.2.4"),
            (true, "Matches_*", "some-cargo-package", "1.2.*"),
            (false, "DoesntMatch_*", "some-cargo-package", "1.1.*"),
            (false, "DoesntMatch_*", "some-cargo-package", "1.1.*"),
            (true, "Matches_^", "some-cargo-package", "^1.1.0"),
            (false, "DoesntMatch_^", "some-cargo-package", "^1.3.0"),
            (true, "Matches_-", "some-cargo-package", "1.2.0 - 1.2.5"),
            (false, "DoesntMatch_-", "some-cargo-package", "1.1.0 - 1.1.5"),
        };
        this.DoAllTheTests(testCases);
    }

    public void DoAllTheTests(IEnumerable<(bool ShouldMatch, string CaseName, string SpecifierName, string SpecifierRange)> testCases)
    {
        foreach (var (shouldMatch, caseName, specifierName, specifierRange) in testCases)
        {
            var di = new DependencySpecification();
            if (specifierName != null)
            {
                di.Add(specifierName, specifierRange);
            }

            di.MatchesPackage(new CargoPackage { Name = "some-cargo-package", Version = "1.2.3" })
                .Should().Be(shouldMatch, caseName);
        }
    }
}
