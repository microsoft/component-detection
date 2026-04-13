namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Generic;
using System.Linq;

public static class PatternMatchingUtility
{
    public delegate bool FilePatternMatcher(ReadOnlySpan<char> span);

    public static FilePatternMatcher GetFilePatternMatcher(IEnumerable<string> patterns)
    {
        var matchers = patterns.Select<string, FilePatternMatcher>(pattern => pattern switch
        {
            _ when pattern.StartsWith('*') && pattern.EndsWith('*') =>
                pattern.Length <= 2
                    ? _ => true
                    : span => span.Contains(pattern.AsSpan(1, pattern.Length - 2), StringComparison.Ordinal),
            _ when pattern.StartsWith('*') =>
                span => span.EndsWith(pattern.AsSpan(1), StringComparison.Ordinal),
            _ when pattern.EndsWith('*') =>
                span => span.StartsWith(pattern.AsSpan(0, pattern.Length - 1), StringComparison.Ordinal),
            _ => span => span.Equals(pattern.AsSpan(), StringComparison.Ordinal),
        }).ToList();

        return span =>
        {
            foreach (var matcher in matchers)
            {
                if (matcher(span))
                {
                    return true;
                }
            }

            return false;
        };
    }
}
