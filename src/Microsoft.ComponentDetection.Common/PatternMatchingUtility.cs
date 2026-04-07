namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;

public static class PatternMatchingUtility
{
    public static bool MatchesPattern(string pattern, string fileName) =>
        FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true);

    public static string? GetMatchingPattern(string fileName, IEnumerable<string> patterns)
    {
        var span = fileName.AsSpan();
        foreach (var pattern in patterns)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, span, ignoreCase: true))
            {
                return pattern;
            }
        }

        return null;
    }

    internal static CompiledMatcher Compile(IEnumerable<string> patterns) => new(patterns);

    internal sealed class CompiledMatcher
    {
        private readonly string[] patterns;

        public CompiledMatcher(IEnumerable<string> patterns) =>
            this.patterns = patterns.ToArray();

        public bool IsMatch(ReadOnlySpan<char> fileName)
        {
            foreach (var pattern in this.patterns)
            {
                if (FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true))
                {
                    return true;
                }
            }

            return false;
        }

        public string? GetMatchingPattern(ReadOnlySpan<char> fileName)
        {
            foreach (var pattern in this.patterns)
            {
                if (FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true))
                {
                    return pattern;
                }
            }

            return null;
        }
    }
}
