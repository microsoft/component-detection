// This file was copied from the SemanticVersioning package found at https://github.com/adamreeve/semver.net.
// The range logic from SemanticVersioning is needed in the Rust detector to supplement the Semver versioning package
// that is used elsewhere in this project.
//
// This is a temporary solution, so avoid using this functionality outside of the Rust detector. The following
// issues describe the problems with the SemanticVersioning package that make it problematic to use for versioning.
// https://github.com/adamreeve/semver.net/issues/46
// https://github.com/adamreeve/semver.net/issues/47

using System;
using System.Text.RegularExpressions;
using Semver;

namespace Microsoft.ComponentDetection.Detectors.Rust.SemVer
{
    internal class Comparator : IEquatable<Comparator>
    {
        public readonly Operator ComparatorType;

        public readonly SemVersion Version;

        private const string RangePattern = @"
            \s*
            ([=<>]*)                # Comparator type (can be empty)
            \s*
            ([0-9a-zA-Z\-\+\.\*]+)  # Version (potentially partial version)
            \s*
            ";

        private static readonly Regex RangePatternRegex = new Regex(
            RangePattern,
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);

        public Comparator(string input)
        {
            var match = RangePatternRegex.Match(input);
            if (!match.Success)
            {
                throw new ArgumentException(string.Format("Invalid comparator string: {0}", input));
            }

            this.ComparatorType = ParseComparatorType(match.Groups[1].Value);
            var partialVersion = new PartialVersion(match.Groups[2].Value);

            if (!partialVersion.IsFull())
            {
                // For Operator.Equal, partial versions are handled by the StarRange
                // desugarer, and desugar to multiple comparators.
                switch (this.ComparatorType)
                {
                    // For <= with a partial version, eg. <=1.2.x, this
                    // means the same as < 1.3.0, and <=1.x means <2.0
                    case Operator.LessThanOrEqual:
                        this.ComparatorType = Operator.LessThan;
                        if (!partialVersion.Major.HasValue)
                        {
                            // <=* means >=0.0.0
                            this.ComparatorType = Operator.GreaterThanOrEqual;
                            this.Version = new SemVersion(0, 0, 0);
                        }
                        else if (!partialVersion.Minor.HasValue)
                        {
                            this.Version = new SemVersion(partialVersion.Major.Value + 1, 0, 0);
                        }
                        else
                        {
                            this.Version = new SemVersion(partialVersion.Major.Value, partialVersion.Minor.Value + 1, 0);
                        }

                        break;
                    case Operator.GreaterThan:
                        this.ComparatorType = Operator.GreaterThanOrEqual;
                        if (!partialVersion.Major.HasValue)
                        {
                            // >* is unsatisfiable, so use <0.0.0
                            this.ComparatorType = Operator.LessThan;
                            this.Version = new SemVersion(0, 0, 0);
                        }
                        else if (!partialVersion.Minor.HasValue)
                        {
                            // eg. >1.x -> >=2.0
                            this.Version = new SemVersion(partialVersion.Major.Value + 1, 0, 0);
                        }
                        else
                        {
                            // eg. >1.2.x -> >=1.3
                            this.Version = new SemVersion(partialVersion.Major.Value, partialVersion.Minor.Value + 1, 0);
                        }

                        break;
                    default:
                        // <1.2.x means <1.2.0
                        // >=1.2.x means >=1.2.0
                        this.Version = partialVersion.ToZeroVersion();
                        break;
                }
            }
            else
            {
                this.Version = partialVersion.ToZeroVersion();
            }
        }

        public Comparator(Operator comparatorType, SemVersion comparatorVersion)
        {
            this.ComparatorType = comparatorType;
            this.Version = comparatorVersion ?? throw new NullReferenceException("Null comparator version");
        }

        public enum Operator
        {
            Equal = 0,
            LessThan,
            LessThanOrEqual,
            GreaterThan,
            GreaterThanOrEqual,
        }

        public static Tuple<int, Comparator> TryParse(string input)
        {
            var match = RangePatternRegex.Match(input);

            return match.Success ?
                Tuple.Create(
                    match.Length,
                    new Comparator(match.Value))
                : null;
        }

        public bool IsSatisfied(SemVersion version)
        {
            return this.ComparatorType switch
            {
                Operator.Equal => version == this.Version,
                Operator.LessThan => version < this.Version,
                Operator.LessThanOrEqual => version <= this.Version,
                Operator.GreaterThan => version > this.Version,
                Operator.GreaterThanOrEqual => version >= this.Version,
                _ => throw new InvalidOperationException("Comparator type not recognised."),
            };
        }

        public bool Intersects(Comparator other)
        {
            static bool OperatorIsGreaterThan(Comparator c) =>
                c.ComparatorType == Operator.GreaterThan ||
                c.ComparatorType == Operator.GreaterThanOrEqual;
            static bool OperatorIsLessThan(Comparator c) =>
                c.ComparatorType == Operator.LessThan ||
                c.ComparatorType == Operator.LessThanOrEqual;
            static bool OperatorIncludesEqual(Comparator c) =>
                c.ComparatorType == Operator.GreaterThanOrEqual ||
                c.ComparatorType == Operator.Equal ||
                c.ComparatorType == Operator.LessThanOrEqual;

            if (this.Version > other.Version && (OperatorIsLessThan(this) || OperatorIsGreaterThan(other)))
            {
                return true;
            }

            if (this.Version < other.Version && (OperatorIsGreaterThan(this) || OperatorIsLessThan(other)))
            {
                return true;
            }

            if (this.Version == other.Version && (
                (OperatorIncludesEqual(this) && OperatorIncludesEqual(other)) ||
                (OperatorIsLessThan(this) && OperatorIsLessThan(other)) ||
                (OperatorIsGreaterThan(this) && OperatorIsGreaterThan(other))))
            {
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            string operatorString = null;
            operatorString = this.ComparatorType switch
            {
                Operator.Equal => "=",
                Operator.LessThan => "<",
                Operator.LessThanOrEqual => "<=",
                Operator.GreaterThan => ">",
                Operator.GreaterThanOrEqual => ">=",
                _ => throw new InvalidOperationException("Comparator type not recognised."),
            };
            return string.Format("{0}{1}", operatorString, this.Version);
        }

        public bool Equals(Comparator other)
        {
            if (other is null)
            {
                return false;
            }

            return this.ComparatorType == other.ComparatorType && this.Version == other.Version;
        }

        public override bool Equals(object other)
        {
            return this.Equals(other as Comparator);
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        private static Operator ParseComparatorType(string input)
        {
            return input switch
            {
                "" or "=" => Operator.Equal,
                "<" => Operator.LessThan,
                "<=" => Operator.LessThanOrEqual,
                ">" => Operator.GreaterThan,
                ">=" => Operator.GreaterThanOrEqual,
                _ => throw new ArgumentException(string.Format("Invalid comparator type: {0}", input)),
            };
        }
    }
}
