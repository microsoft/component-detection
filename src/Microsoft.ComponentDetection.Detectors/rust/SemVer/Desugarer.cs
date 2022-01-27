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
    internal static class Desugarer
    {
        private const string VersionChars = @"[0-9a-zA-Z\-\+\.\*]";

        // tilde and caret requirements can't also have wildcards in them 
        private const string VersionCharsNoWildcard = @"[0-9a-zA-Z\-\+\.]";

        private static readonly Regex TildePatternRegex = new Regex(
            $@"^\s*~\s*({VersionCharsNoWildcard}+)\s*$",
            RegexOptions.Compiled);

        // The caret is optional, as Cargo treats "x.y.z" like "^x.y.z":
        // https://doc.rust-lang.org/cargo/reference/specifying-dependencies.html#specifying-dependencies-from-cratesio
        private static readonly Regex CaretPatternRegex = new Regex(
           $@"^\s*\^?\s*({VersionCharsNoWildcard}+)\s*$",
           RegexOptions.Compiled);

        private static readonly Regex HyphenPatternRegex = new Regex(
            $@"^\s*({VersionChars}+)\s+\-\s+({VersionChars}+)\s*",
            RegexOptions.Compiled);

        private static readonly Regex StarPatternRegex = new Regex(
            $@"^\s*=?\s*({VersionChars}+)\s*",
            RegexOptions.Compiled);

        // Allows patch-level changes if a minor version is specified
        // on the comparator. Allows minor-level changes if not.
        public static Tuple<int, Comparator[]> TildeRange(string spec)
        {
            var match = TildePatternRegex.Match(spec);
            if (!match.Success)
            {
                return null;
            }

            SemVersion minVersion = null;
            SemVersion maxVersion = null;

            var version = new PartialVersion(match.Groups[1].Value);
            if (version.Minor.HasValue)
            {
                // Doesn't matter whether patch version is null or not,
                // the logic is the same, min patch version will be zero if null.
                minVersion = version.ToZeroVersion();
                maxVersion = new SemVersion(version.Major.Value, version.Minor.Value + 1, 0);
            }
            else
            {
                minVersion = version.ToZeroVersion();
                maxVersion = new SemVersion(version.Major.Value + 1, 0, 0);
            }

            return Tuple.Create(
                    match.Length,
                    MinMaxComparators(minVersion, maxVersion));
        }

        // Allows changes that do not modify the left-most non-zero digit
        // in the [major, minor, patch] tuple.
        public static Tuple<int, Comparator[]> CaretRange(string spec)
        {
            var match = CaretPatternRegex.Match(spec);
            if (!match.Success)
            {
                return null;
            }

            SemVersion minVersion = null;
            SemVersion maxVersion = null;

            var version = new PartialVersion(match.Groups[1].Value);

            if (version.Major.Value > 0)
            {
                // Don't allow major version change
                minVersion = version.ToZeroVersion();
                maxVersion = new SemVersion(version.Major.Value + 1, 0, 0);
            }
            else if (!version.Minor.HasValue)
            {
                // Don't allow major version change, even if it's zero
                minVersion = version.ToZeroVersion();
                maxVersion = new SemVersion(version.Major.Value + 1, 0, 0);
            }
            else if (!version.Patch.HasValue)
            {
                // Don't allow minor version change, even if it's zero
                minVersion = version.ToZeroVersion();
                maxVersion = new SemVersion(0, version.Minor.Value + 1, 0);
            }
            else if (version.Minor > 0)
            {
                // Don't allow minor version change
                minVersion = version.ToZeroVersion();
                maxVersion = new SemVersion(0, version.Minor.Value + 1, 0);
            }
            else
            {
                // Only patch non-zero, don't allow patch change
                minVersion = version.ToZeroVersion();
                maxVersion = new SemVersion(0, 0, version.Patch.Value + 1);
            }

            return Tuple.Create(
                    match.Length,
                    MinMaxComparators(minVersion, maxVersion));
        }

        public static Tuple<int, Comparator[]> HyphenRange(string spec)
        {
            var match = HyphenPatternRegex.Match(spec);
            if (!match.Success)
            {
                return null;
            }

            PartialVersion minPartialVersion = null;
            PartialVersion maxPartialVersion = null;

            // Parse versions from lower and upper ranges, which might
            // be partial versions.
            try
            {
                minPartialVersion = new PartialVersion(match.Groups[1].Value);
                maxPartialVersion = new PartialVersion(match.Groups[2].Value);
            }
            catch (ArgumentException)
            {
                return null;
            }

            // Lower range has any non-supplied values replaced with zero
            var minVersion = minPartialVersion.ToZeroVersion();

            Comparator.Operator maxOperator = maxPartialVersion.IsFull()
                ? Comparator.Operator.LessThanOrEqual : Comparator.Operator.LessThan;

            SemVersion maxVersion = null;

            // Partial upper range means supplied version values can't change
            if (!maxPartialVersion.Major.HasValue)
            {
                // eg. upper range = "*", then maxVersion remains null
                // and there's only a minimum
            }
            else if (!maxPartialVersion.Minor.HasValue)
            {
                maxVersion = new SemVersion(maxPartialVersion.Major.Value + 1, 0, 0);
            }
            else if (!maxPartialVersion.Patch.HasValue)
            {
                maxVersion = new SemVersion(maxPartialVersion.Major.Value, maxPartialVersion.Minor.Value + 1, 0);
            }
            else
            {
                // Fully specified max version
                maxVersion = maxPartialVersion.ToZeroVersion();
            }

            return Tuple.Create(
                    match.Length,
                    MinMaxComparators(minVersion, maxVersion, maxOperator));
        }

        public static Tuple<int, Comparator[]> StarRange(string spec)
        {
            var match = StarPatternRegex.Match(spec);

            if (!match.Success)
            {
                return null;
            }

            PartialVersion version = null;
            try
            {
                version = new PartialVersion(match.Groups[1].Value);
            }
            catch (ArgumentException)
            {
                return null;
            }

            // If partial version match is actually a full version,
            // then this isn't a star range, so return null.
            if (version.IsFull())
            {
                return null;
            }

            SemVersion minVersion = null;
            SemVersion maxVersion = null;

            if (!version.Major.HasValue)
            {
                minVersion = version.ToZeroVersion();

                // no max version
            }
            else if (!version.Minor.HasValue)
            {
                minVersion = version.ToZeroVersion();
                maxVersion = new SemVersion(version.Major.Value + 1, 0, 0);
            }
            else
            {
                minVersion = version.ToZeroVersion();
                maxVersion = new SemVersion(version.Major.Value, version.Minor.Value + 1, 0);
            }

            return Tuple.Create(
                    match.Length,
                    MinMaxComparators(minVersion, maxVersion));
        }

        private static Comparator[] MinMaxComparators(SemVersion minVersion, SemVersion maxVersion,
                Comparator.Operator maxOperator = Comparator.Operator.LessThan)
        {
            var minComparator = new Comparator(
                    Comparator.Operator.GreaterThanOrEqual,
                    minVersion);
            if (maxVersion == null)
            {
                return new[] { minComparator };
            }
            else
            {
                var maxComparator = new Comparator(
                        maxOperator, maxVersion);
                return new[] { minComparator, maxComparator };
            }
        }
    }
}
