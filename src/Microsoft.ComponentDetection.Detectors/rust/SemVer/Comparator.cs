// This file was copied from the SemanticVersioning package found at https://github.com/adamreeve/semver.net.
// The range logic from SemanticVersioning is needed in the Rust detector to supplement the Semver versioning package
// that is used elsewhere in this project.
//
// This is a temporary solution, so avoid using this functionality outside of the Rust detector. The following
// issues describe the problems with the SemanticVersioning package that make it problematic to use for versioning.
// https://github.com/adamreeve/semver.net/issues/46
// https://github.com/adamreeve/semver.net/issues/47

namespace Microsoft.ComponentDetection.Detectors.Rust.SemVer
{
    using System;
    using System.Text.RegularExpressions;
    using Semver;

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
            if (comparatorVersion == null)
            {
                throw new NullReferenceException("Null comparator version");
            }

            this.ComparatorType = comparatorType;
            this.Version = comparatorVersion;
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

        private static Operator ParseComparatorType(string input)
        {
            switch (input)
            {
                case "":
                case "=":
                    return Operator.Equal;
                case "<":
                    return Operator.LessThan;
                case "<=":
                    return Operator.LessThanOrEqual;
                case ">":
                    return Operator.GreaterThan;
                case ">=":
                    return Operator.GreaterThanOrEqual;
                default:
                    throw new ArgumentException(string.Format("Invalid comparator type: {0}", input));
            }
        }

        public bool IsSatisfied(SemVersion version)
        {
            switch (this.ComparatorType)
            {
                case Operator.Equal:
                    return version == this.Version;
                case Operator.LessThan:
                    return version < this.Version;
                case Operator.LessThanOrEqual:
                    return version <= this.Version;
                case Operator.GreaterThan:
                    return version > this.Version;
                case Operator.GreaterThanOrEqual:
                    return version >= this.Version;
                default:
                    throw new InvalidOperationException("Comparator type not recognised.");
            }
        }

        public bool Intersects(Comparator other)
        {
            Func<Comparator, bool> operatorIsGreaterThan = c =>
                c.ComparatorType == Operator.GreaterThan ||
                c.ComparatorType == Operator.GreaterThanOrEqual;
            Func<Comparator, bool> operatorIsLessThan = c =>
                c.ComparatorType == Operator.LessThan ||
                c.ComparatorType == Operator.LessThanOrEqual;
            Func<Comparator, bool> operatorIncludesEqual = c =>
                c.ComparatorType == Operator.GreaterThanOrEqual ||
                c.ComparatorType == Operator.Equal ||
                c.ComparatorType == Operator.LessThanOrEqual;

            if (this.Version > other.Version && (operatorIsLessThan(this) || operatorIsGreaterThan(other)))
            {
                return true;
            }

            if (this.Version < other.Version && (operatorIsGreaterThan(this) || operatorIsLessThan(other)))
            {
                return true;
            }

            if (this.Version == other.Version && (
                (operatorIncludesEqual(this) && operatorIncludesEqual(other)) ||
                (operatorIsLessThan(this) && operatorIsLessThan(other)) ||
                (operatorIsGreaterThan(this) && operatorIsGreaterThan(other))))
            {
                return true;
            }

            return false;
        }

        public enum Operator
        {
            Equal = 0,
            LessThan,
            LessThanOrEqual,
            GreaterThan,
            GreaterThanOrEqual,
        }

        public override string ToString()
        {
            string operatorString = null;
            switch (this.ComparatorType)
            {
                case Operator.Equal:
                    operatorString = "=";
                    break;
                case Operator.LessThan:
                    operatorString = "<";
                    break;
                case Operator.LessThanOrEqual:
                    operatorString = "<=";
                    break;
                case Operator.GreaterThan:
                    operatorString = ">";
                    break;
                case Operator.GreaterThanOrEqual:
                    operatorString = ">=";
                    break;
                default:
                    throw new InvalidOperationException("Comparator type not recognised.");
            }

            return string.Format("{0}{1}", operatorString, this.Version);
        }

        public bool Equals(Comparator other)
        {
            if (ReferenceEquals(other, null))
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
    }
}
