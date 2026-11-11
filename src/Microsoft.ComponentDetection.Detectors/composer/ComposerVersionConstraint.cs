namespace Microsoft.ComponentDetection.Detectors.Composer;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>Compares virtual-package declarations without inventing a version for the virtual package.</summary>
/// <remarks>
/// Local patterns implement a subset of the syntax in
/// <see href="https://github.com/composer/semver/blob/main/src/VersionParser.php">Composer's upstream version parser</see>.
/// </remarks>
internal static class ComposerVersionConstraint
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(3);
    private static readonly Regex LockedBranchRegex = new(
        @"^(?<prefix>\d+(?:\.\d+){0,2})\.[xX*](?:\.[xX*])*-dev$",
        RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex StabilityFlagRegex = new(
        @"@(dev|alpha|beta|RC|stable)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        RegexTimeout);

    private static readonly Regex HyphenRangeRegex = new(
        @"^\s*(\S+)\s+-\s+(\S+)\s*$",
        RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex ConstraintTokenRegex = new(
        @"^(?<op>!=|<>|>=|<=|==|[=><~^])?\s*(?<version>[^\s]+)(?: +as +[^,\s]+)?",
        RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex WildcardVersionRegex = new(
        @"^[vV]?(?<prefix>\d+(?:\.\d+){0,2})\.(?:[xX*])(?:\.[xX*])*(?:-dev)?$",
        RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex NumericVersionRegex = new(
        @"^[vV]?(?<number>\d+(?:\.\d+){0,3})(?:[._-]?(?<stability>dev|alpha|beta|rc|stable|patch|pl|p|a|b)[.-]?(?<suffix>\d*))?(?:\+[a-zA-Z0-9.-]+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        RegexTimeout);

    internal static bool TryGetExactVersion(string constraint, out string version)
    {
        version = constraint.Trim();
        if (version.StartsWith("==", StringComparison.Ordinal))
        {
            version = version[2..].TrimStart();
        }
        else if (version.StartsWith('='))
        {
            version = version[1..].TrimStart();
        }

        // Branch names and aliases do not identify an installed revision without a lockfile.
        return TryVersion(version, out _, out _, out _);
    }

    internal static bool Intersects(string required, string provided) =>
        TryIntersects(required, provided, out var intersects) && intersects;

    internal static bool TryIntersects(string required, string provided, out bool intersects)
    {
        intersects = false;
        if (!TryParse(required, out var requirement) || !TryParse(provided, out var provision))
        {
            return false;
        }

        intersects = requirement.Any(left => provision.Any(right => left.Intersect(right).IsNonempty));
        return true;
    }

    internal static bool VersionsEqual(string left, string right)
    {
        left = left.Trim();
        right = right.Trim();
        return left.Equals(right, StringComparison.Ordinal) ||
            (TryLockedVersion(left, out var first) && TryLockedVersion(right, out var second) && first == second);
    }

    private static bool TryLockedVersion(string text, out ComposerVersion version)
    {
        if (TryVersion(text, out version, out _, out _))
        {
            return true;
        }

        var branch = LockedBranchRegex.Match(text);
        if (!branch.Success)
        {
            return false;
        }

        var segments = branch.Groups["prefix"].Value.Split('.');
        var normalized = string.Join('.', segments.Concat(Enumerable.Repeat("9999999", 4 - segments.Length)));
        return TryVersion($"{normalized}-dev", out version, out _, out _);
    }

    private static bool TryParse(string text, out List<VersionSet> alternatives)
    {
        alternatives = [];
        text = StabilityFlagRegex.Replace(text.Trim(), string.Empty);
        foreach (var alternative in text.Split(["||", "|"], StringSplitOptions.TrimEntries))
        {
            // Hyphen bounds retain Composer's precision-sensitive inclusive upper limit.
            var hyphen = HyphenRangeRegex.Match(alternative);
            if (hyphen.Success)
            {
                if (!TryVersion(hyphen.Groups[1].Value, out var lower, out _, out var lowerSuffix) ||
                    !TryVersion(hyphen.Groups[2].Value, out var upper, out var precision, out _))
                {
                    return false;
                }

                if (precision < 3)
                {
                    if (!TryUpperBound(upper, precision - 1, out upper))
                    {
                        return false;
                    }

                    alternatives.Add(VersionSet.Numeric(new Interval(lowerSuffix ? lower : lower.WithDevStability(), true, upper, false)));
                }
                else
                {
                    alternatives.Add(VersionSet.Numeric(new Interval(lowerSuffix ? lower : lower.WithDevStability(), true, upper, true)));
                }

                continue;
            }

            var remaining = alternative.Replace(',', ' ').Trim();
            var result = VersionSet.All();
            if (remaining.Length == 0)
            {
                return false;
            }

            while (remaining.Length > 0)
            {
                // Native parseConstraint compares an inline alias's source, not its display alias.
                // Consume aliasing per constraint so surrounding AND/OR terms remain intact.
                var token = ConstraintTokenRegex.Match(remaining);
                if (!token.Success || !TryToken(token.Groups["op"].Value, token.Groups["version"].Value, out var constraint))
                {
                    return false;
                }

                result = result.Intersect(constraint);
                remaining = remaining[token.Length..].TrimStart();
            }

            alternatives.Add(result);
        }

        return true;
    }

    private static bool TryToken(string operation, string text, out VersionSet result)
    {
        result = VersionSet.Empty();
        var reference = text.IndexOf('#', StringComparison.Ordinal);
        if (reference >= 0)
        {
            text = text[..reference];
        }

        if (text is "*" or "x" or "X")
        {
            if (operation is "" or "=" or "==")
            {
                result = VersionSet.All();
                return true;
            }

            return false;
        }

        if (text.StartsWith("dev-", StringComparison.Ordinal) && text.Length > 4)
        {
            if (operation is "" or "=" or "==")
            {
                result.Branch = text;
                return true;
            }

            if (operation is "!=" or "<>")
            {
                result = VersionSet.All();
                result.ExcludedBranches.Add(text);
                return true;
            }

            return false;
        }

        var wildcard = WildcardVersionRegex.Match(text);
        if (wildcard.Success)
        {
            if (operation is not ("" or "=" or "==") ||
                !TryVersion(wildcard.Groups["prefix"].Value, out var minimum, out var precision, out _) ||
                !TryUpperBound(minimum, precision - 1, out var maximum))
            {
                return false;
            }

            result = VersionSet.Numeric(new Interval(minimum.WithDevStability(), true, maximum, false));
            return true;
        }

        if (!TryVersion(text, out var version, out var parts, out var hasSuffix))
        {
            return false;
        }

        // Composer's implicit >= and < bounds use dev stability; npm-style ranges differ.
        var bound = !hasSuffix && operation is ">=" or "<" ? version.WithDevStability() : version;
        switch (operation)
        {
            case "":
            case "=":
            case "==":
                result = VersionSet.Numeric(new Interval(version, true, version, true));
                break;
            case "!=":
            case "<>":
                result = VersionSet.All();
                result.Intervals = [new Interval(null, false, version, false), new Interval(version, false, null, false)];
                break;
            case ">":
            case ">=":
                result = VersionSet.Numeric(new Interval(bound, operation == ">=", null, false));
                break;
            case "<":
            case "<=":
                result = VersionSet.Numeric(new Interval(null, false, bound, operation == "<="));
                break;
            case "~":
            case "^":
                var index = operation == "~" ? Math.Max(0, parts - 2) :
                    version.Major != 0 || parts == 1 ? 0 : version.Minor != 0 || parts == 2 ? 1 : 2;
                if (!TryUpperBound(version, index, out var upper))
                {
                    return false;
                }

                result = VersionSet.Numeric(new Interval(hasSuffix ? version : version.WithDevStability(), true, upper, false));
                break;
            default:
                return false;
        }

        return true;
    }

    private static bool TryVersion(string text, out ComposerVersion version, out int precision, out bool hasSuffix)
    {
        version = default;
        precision = 0;
        hasSuffix = false;
        var match = NumericVersionRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        var segments = match.Groups["number"].Value.Split('.');
        var numbers = new long[4];
        for (var index = 0; index < segments.Length; index++)
        {
            if (!long.TryParse(segments[index], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return false;
            }
        }

        hasSuffix = match.Groups["stability"].Success;
        var stability = match.Groups["stability"].Value.ToUpperInvariant() switch
        {
            "DEV" => 0,
            "ALPHA" or "A" => 1,
            "BETA" or "B" => 2,
            "RC" => 3,
            "PATCH" or "PL" or "P" => 5,
            _ => 4,
        };
        var suffix = 0L;
        if (match.Groups["suffix"].Value.Length > 0 &&
            !long.TryParse(match.Groups["suffix"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out suffix))
        {
            return false;
        }

        precision = segments.Length;
        version = new ComposerVersion(numbers[0], numbers[1], numbers[2], numbers[3], stability, suffix);
        return true;
    }

    private static bool TryUpperBound(ComposerVersion version, int index, out ComposerVersion bound)
    {
        long[] numbers = [version.Major, version.Minor, version.Patch, version.Revision];
        bound = default;
        if (numbers[index] == long.MaxValue)
        {
            return false;
        }

        numbers[index]++;
        Array.Clear(numbers, index + 1, numbers.Length - index - 1);
        bound = new ComposerVersion(numbers[0], numbers[1], numbers[2], numbers[3], 0, 0);
        return true;
    }

    private readonly record struct ComposerVersion(long Major, long Minor, long Patch, long Revision, int Stability, long Suffix)
    {
        internal ComposerVersion WithDevStability() => this with { Stability = 0, Suffix = 0 };

        internal int CompareTo(ComposerVersion other)
        {
            var comparison = this.Major.CompareTo(other.Major);
            comparison = comparison != 0 ? comparison : this.Minor.CompareTo(other.Minor);
            comparison = comparison != 0 ? comparison : this.Patch.CompareTo(other.Patch);
            comparison = comparison != 0 ? comparison : this.Revision.CompareTo(other.Revision);
            comparison = comparison != 0 ? comparison : this.Stability.CompareTo(other.Stability);
            return comparison != 0 ? comparison : this.Suffix.CompareTo(other.Suffix);
        }
    }

    private readonly record struct Interval(ComposerVersion? Lower, bool LowerInclusive, ComposerVersion? Upper, bool UpperInclusive)
    {
        internal bool IsNonempty => this.Lower is not { } lower || this.Upper is not { } upper ||
            lower.CompareTo(upper) < 0 || (lower == upper && this.LowerInclusive && this.UpperInclusive);

        internal Interval Intersect(Interval other)
        {
            var lower = this.Lower;
            var lowerInclusive = this.LowerInclusive;
            if (lower is null || (other.Lower is { } otherLower && otherLower.CompareTo(lower.Value) > 0))
            {
                lower = other.Lower;
                lowerInclusive = other.LowerInclusive;
            }
            else if (lower == other.Lower)
            {
                lowerInclusive &= other.LowerInclusive;
            }

            var upper = this.Upper;
            var upperInclusive = this.UpperInclusive;
            if (upper is null || (other.Upper is { } otherUpper && otherUpper.CompareTo(upper.Value) < 0))
            {
                upper = other.Upper;
                upperInclusive = other.UpperInclusive;
            }
            else if (upper == other.Upper)
            {
                upperInclusive &= other.UpperInclusive;
            }

            return new Interval(lower, lowerInclusive, upper, upperInclusive);
        }
    }

    private sealed class VersionSet
    {
        internal List<Interval> Intervals { get; set; } = [];

        internal string? Branch { get; set; }

        internal HashSet<string> ExcludedBranches { get; } = [];

        internal bool IsNonempty => this.Intervals.Count > 0 || (this.Branch is not null && !this.ExcludedBranches.Contains(this.Branch));

        internal static VersionSet All() => new() { Intervals = [new Interval(default(ComposerVersion), true, null, false)], Branch = "*" };

        internal static VersionSet Empty() => new();

        internal static VersionSet Numeric(Interval interval) => new() { Intervals = interval.IsNonempty ? [interval] : [] };

        internal VersionSet Intersect(VersionSet other)
        {
            var result = new VersionSet
            {
                Intervals = this.Intervals.SelectMany(left => other.Intervals.Select(left.Intersect)).Where(interval => interval.IsNonempty).ToList(),
                Branch = this.Branch == "*" ? other.Branch : other.Branch == "*" || this.Branch == other.Branch ? this.Branch : null,
            };
            result.ExcludedBranches.UnionWith(this.ExcludedBranches);
            result.ExcludedBranches.UnionWith(other.ExcludedBranches);
            return result;
        }
    }
}
