using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class RustDependencySpecifierTest
    {
        [TestMethod]
        public void DoesntMatch_WhenNoDependencyAdded()
        {
            var testCases = new List<(bool shouldMatch, string caseName, string specifierName, string specifierRange)>
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

        public void DoAllTheTests(IEnumerable<(bool shouldMatch, string caseName, string specifierName, string specifierRange)> testCases)
        {
            foreach (var testCase in testCases)
            {
                var di = new DependencySpecification();
                if (testCase.specifierName != null)
                {
                    di.Add(testCase.specifierName, testCase.specifierRange);
                }

                di.MatchesPackage(new CargoPackage { name = "some-cargo-package", version = "1.2.3" })
                    .Should().Be(testCase.shouldMatch, testCase.caseName);
            }
        }
    }
}
