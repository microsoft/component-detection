// This file was copied from the SemanticVersioning package found at https://github.com/adamreeve/semver.net.
// The range logic from SemanticVersioning is needed in the Rust detector to supplement the Semver versioning package
// that is used elsewhere in this project.
//
// This is a temporary solution, so avoid using this functionality outside of the Rust detector. The following
// issues describe the problems with the SemanticVersioning package that make it problematic to use for versioning.
// https://github.com/adamreeve/semver.net/issues/46
// https://github.com/adamreeve/semver.net/issues/47

using System;
using System.Collections.Generic;
using System.Linq;
using Semver;

namespace Microsoft.ComponentDetection.Detectors.Rust.SemVer
{
    internal class ComparatorSet : IEquatable<ComparatorSet>
    {
        private readonly List<Comparator> comparators;

        public ComparatorSet(string spec)
        {
            comparators = new List<Comparator> { };

            spec = spec.Trim();
            if (spec == string.Empty)
            {
                spec = "*";
            }

            int position = 0;
            int end = spec.Length;

            while (position < end)
            {
                int iterStartPosition = position;

                // A comparator set might be an advanced range specifier
                // like ~1.2.3, ^1.2, or 1.*.
                // Check for these first before standard comparators:
                foreach (var desugarer in new Func<string, Tuple<int, Comparator[]>>[]
                {
                    Desugarer.HyphenRange,
                    Desugarer.TildeRange,
                    Desugarer.CaretRange,
                    Desugarer.StarRange,
                })
                {
                    var result = desugarer(spec.Substring(position));
                    if (result != null)
                    {
                        position += result.Item1;
                        comparators.AddRange(result.Item2);
                    }
                }

                // Check for standard comparator with operator and version:
                var comparatorResult = Comparator.TryParse(spec.Substring(position));
                if (comparatorResult != null)
                {
                    position += comparatorResult.Item1;
                    comparators.Add(comparatorResult.Item2);
                }

                if (position == iterStartPosition)
                {
                    // Didn't manage to read any valid comparators
                    throw new ArgumentException(string.Format("Invalid range specification: \"{0}\"", spec));
                }
            }
        }

        private ComparatorSet(IEnumerable<Comparator> comparators)
        {
            this.comparators = comparators.ToList();
        }

        public bool IsSatisfied(SemVersion version)
        {
            bool satisfied = comparators.All(c => c.IsSatisfied(version));
            if (version.Prerelease != string.Empty)
            {
                // If the version is a pre-release, then one of the
                // comparators must have the same version and also include
                // a pre-release tag.
                return satisfied && comparators.Any(c =>
                        c.Version.Prerelease != string.Empty &&
                        c.Version.Major == version.Major &&
                        c.Version.Minor == version.Minor &&
                        c.Version.Patch == version.Patch);
            }
            else
            {
                return satisfied;
            }
        }

        public ComparatorSet Intersect(ComparatorSet other)
        {
            Func<Comparator, bool> operatorIsGreaterThan = c =>
                c.ComparatorType == Comparator.Operator.GreaterThan ||
                c.ComparatorType == Comparator.Operator.GreaterThanOrEqual;
            Func<Comparator, bool> operatorIsLessThan = c =>
                c.ComparatorType == Comparator.Operator.LessThan ||
                c.ComparatorType == Comparator.Operator.LessThanOrEqual;
            var maxOfMins =
                this.comparators.Concat(other.comparators)
                .Where(operatorIsGreaterThan)
                .OrderByDescending(c => c.Version).FirstOrDefault();
            var minOfMaxs =
                this.comparators.Concat(other.comparators)
                .Where(operatorIsLessThan)
                .OrderBy(c => c.Version).FirstOrDefault();
            if (maxOfMins != null && minOfMaxs != null && !maxOfMins.Intersects(minOfMaxs))
            {
                return null;
            }

            // If there is an equality operator, check that it satisfies other operators
            var equalityVersions =
                this.comparators.Concat(other.comparators)
                .Where(c => c.ComparatorType == Comparator.Operator.Equal)
                .Select(c => c.Version)
                .ToList();
            if (equalityVersions.Count > 1)
            {
                if (equalityVersions.Any(v => v != equalityVersions[0]))
                {
                    return null;
                }
            }

            if (equalityVersions.Count > 0)
            {
                if (maxOfMins != null && !maxOfMins.IsSatisfied(equalityVersions[0]))
                {
                    return null;
                }

                if (minOfMaxs != null && !minOfMaxs.IsSatisfied(equalityVersions[0]))
                {
                    return null;
                }

                return new ComparatorSet(
                    new List<Comparator>
                    {
                        new Comparator(Comparator.Operator.Equal, equalityVersions[0]),
                    });
            }

            var comparators = new List<Comparator>();
            if (maxOfMins != null)
            {
                comparators.Add(maxOfMins);
            }

            if (minOfMaxs != null)
            {
                comparators.Add(minOfMaxs);
            }

            return comparators.Count > 0 ? new ComparatorSet(comparators) : null;
        }

        public bool Equals(ComparatorSet other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            var thisSet = new HashSet<Comparator>(comparators);
            return thisSet.SetEquals(other.comparators);
        }

        public override bool Equals(object other)
        {
            return Equals(other as ComparatorSet);
        }

        public override string ToString()
        {
            return string.Join(" ", comparators.Select(c => c.ToString()).ToArray());
        }

        public override int GetHashCode()
        {
            // XOR is commutative, so this hash code is independent
            // of the order of comparators.
            return comparators.Aggregate(0, (accum, next) => accum ^ next.GetHashCode());
        }
    }
}
