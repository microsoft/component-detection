namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;

public static class PatternMatchingUtility
{
    public static bool MatchesPattern(string pattern, string fileName)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(fileName);

        return IsPatternMatch(pattern, fileName.AsSpan());
    }

    /// <summary>
    /// Returns the first matching pattern for <paramref name="fileName"/>.
    /// Earlier patterns in <paramref name="patterns"/> have higher priority when multiple match.
    /// </summary>
    /// <returns>The first matching pattern, or <see langword="null"/> if no patterns match.</returns>
    public static string? GetMatchingPattern(string fileName, IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(patterns);

        return GetFirstMatchingPattern(fileName.AsSpan(), patterns);
    }

    public static CompiledMatcher Compile(IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return patterns is string[] array ? Compile(array) : new(patterns.ToArray());
    }

    public static CompiledMatcher Compile(string[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return new(patterns);
    }

    private static string? GetFirstMatchingPattern(ReadOnlySpan<char> fileName, IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            ArgumentNullException.ThrowIfNull(pattern);

            if (IsPatternMatch(pattern, fileName))
            {
                return pattern;
            }
        }

        return null;
    }

    /// <summary>
    /// Fast path for pre-validated pattern arrays. Skips per-element null checks.
    /// </summary>
    private static string? GetFirstMatchingPattern(ReadOnlySpan<char> fileName, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (IsPatternMatch(pattern, fileName))
            {
                return pattern;
            }
        }

        return null;
    }

    private static bool IsPatternMatch(string pattern, ReadOnlySpan<char> fileName) =>
        FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true);

    public sealed class CompiledMatcher
    {
        private readonly string[] patterns;

        public CompiledMatcher(IEnumerable<string> patterns)
            : this(patterns?.ToArray()!)
        {
        }

        internal CompiledMatcher(string[] patterns)
        {
            ArgumentNullException.ThrowIfNull(patterns);
            ValidatePatternElements(patterns);
            this.patterns = patterns;
        }

        public bool IsMatch(ReadOnlySpan<char> fileName) => GetFirstMatchingPattern(fileName, this.patterns) is not null;

        /// <summary>
        /// Returns the first matching pattern for <paramref name="fileName"/>.
        /// Earlier patterns in the compiled set have higher priority when multiple match.
        /// </summary>
        /// <returns>The first matching pattern, or <see langword="null"/> if no patterns match.</returns>
        public string? GetMatchingPattern(ReadOnlySpan<char> fileName) => GetFirstMatchingPattern(fileName, this.patterns);

        private static void ValidatePatternElements(IEnumerable<string> patterns)
        {
            foreach (var pattern in patterns)
            {
                ArgumentNullException.ThrowIfNull(pattern);
            }
        }
    }
}
