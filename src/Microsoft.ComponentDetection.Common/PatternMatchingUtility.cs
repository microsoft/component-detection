using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.ComponentDetection.Common
{
    public static class PatternMatchingUtility
    {
        public delegate bool FilePatternMatcher(ReadOnlySpan<char> span);

        public static FilePatternMatcher GetFilePatternMatcher(IEnumerable<string> patterns)
        {
            var ordinalComparison = Expression.Constant(System.StringComparison.Ordinal, typeof(System.StringComparison));
            var asSpan = typeof(System.MemoryExtensions).GetMethod("AsSpan", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, new[] { typeof(string) }, new ParameterModifier[0]);
            var equals = typeof(System.MemoryExtensions).GetMethod("Equals", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, new[] { typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>), typeof(System.StringComparison) }, new ParameterModifier[0]);
            var startsWith = typeof(System.MemoryExtensions).GetMethod("StartsWith", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, new[] { typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>), typeof(System.StringComparison) }, new ParameterModifier[0]);
            var endsWith = typeof(System.MemoryExtensions).GetMethod("EndsWith", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Standard, new[] { typeof(ReadOnlySpan<char>), typeof(ReadOnlySpan<char>), typeof(System.StringComparison) }, new ParameterModifier[0]);

            var predicates = new List<Expression>();
            var left = Expression.Parameter(typeof(ReadOnlySpan<char>), "fileName");

            foreach (var pattern in patterns)
            {
                if (pattern.StartsWith("*"))
                {
                    var match = Expression.Constant(pattern.Substring(1), typeof(string));
                    var right = Expression.Call(null, asSpan, match);
                    var combine = Expression.Call(null, endsWith, left, right, ordinalComparison);
                    predicates.Add(combine);
                }
                else if (pattern.EndsWith("*"))
                {
                    var match = Expression.Constant(pattern.Substring(0, pattern.Length - 1), typeof(string));
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
}