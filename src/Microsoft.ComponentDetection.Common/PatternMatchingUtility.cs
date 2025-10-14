namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

public static class PatternMatchingUtility
{
    public delegate bool FilePatternMatcher(ReadOnlySpan<char> span);

    public static FilePatternMatcher GetFilePatternMatcher(IEnumerable<string> patterns)
    {
        var ordinalComparison = Expression.Constant(StringComparison.Ordinal, typeof(StringComparison));
        var asSpan = typeof(MemoryExtensions).GetMethod("AsSpan", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, [typeof(string)], []);
        var equals = typeof(MemoryExtensions).GetMethod("Equals", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, [typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>), typeof(StringComparison)], []);
        var startsWith = typeof(MemoryExtensions).GetMethod("StartsWith", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, [typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>), typeof(StringComparison)], []);
        var endsWith = typeof(MemoryExtensions).GetMethod("EndsWith", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, [typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>), typeof(StringComparison)], []);

        var predicates = new List<Expression>();
        var left = Expression.Parameter(typeof(ReadOnlySpan<char>), "fileName");

        foreach (var pattern in patterns)
        {
            if (pattern.StartsWith('*'))
            {
                var match = Expression.Constant(pattern[1..], typeof(string));
                var right = Expression.Call(null, asSpan, match);
                var combine = Expression.Call(null, endsWith, left, right, ordinalComparison);
                predicates.Add(combine);
            }
            else if (pattern.EndsWith('*'))
            {
                var match = Expression.Constant(pattern[..^1], typeof(string));
                var right = Expression.Call(null, asSpan, match);
                var combine = Expression.Call(null, startsWith, left, right, ordinalComparison);
                predicates.Add(combine);
            }
            else
            {
                var match = Expression.Constant(pattern, typeof(string));
                var right = Expression.Call(null, asSpan, match);
                var combine = Expression.Call(null, equals, left, right, ordinalComparison);
                predicates.Add(combine);
            }
        }

        var aggregateExpression = predicates.Aggregate(Expression.OrElse);

        var func = Expression.Lambda<FilePatternMatcher>(aggregateExpression, left).Compile();

        return func;
    }
}
