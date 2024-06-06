/*
 This file (and parts of MinimatchTests.cs) is a port of
 https://github.com/isaacs/minimatch/commit/cb4be48a55d64b3a40a745d4a8eb4d1b06507277

 Original license:
   The ISC License

   Copyright (c) 2011-2023 Isaac Z. Schlueter and Contributors

   Permission to use, copy, modify, and/or distribute this software for any
   purpose with or without fee is hereby granted, provided that the above
   copyright notice and this permission notice appear in all copies.

   THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
   WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
   MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
   ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
   WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
   ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR
   IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
 */

namespace Microsoft.ComponentDetection.Orchestrator;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// A minimal matching utility.
/// </summary>
public class Minimatch
{
    // any single thing other than /
    // don't need to escape / when using new RegExp()
    private const string Qmark = "[^/]";

    // any single thing other than /
    // don't need to escape / when using new RegExp()
    private const string Star = Qmark + "*?";

    // characters that need to be escaped in RegExp.
    private static readonly HashSet<char> ReSpecials = new("().*{}+?[]^$\\!".ToCharArray());
    private static readonly Regex SlashSplit = new("/+", RegexOptions.Compiled);

    private static readonly Regex HasBraces = new(@"\{.*\}", RegexOptions.Compiled);
    private static readonly Regex NumericSet = new(@"^\{(-?[0-9]+)\.\.(-?[0-9]+)\}", RegexOptions.Compiled);

    // replace stuff like \* with *
    private static readonly Regex GlobUnescaper = new(@"\\(.)", RegexOptions.Compiled);
    private static readonly Regex EscapeCheck = new(@"((?:\\{2})*)(\\?)\|", RegexOptions.Compiled);

    private readonly bool ignoreCase;
    private readonly bool isWindows;
    private readonly bool negate;
    private readonly bool comment;
    private readonly bool empty;
    private readonly List<List<ParseItem>> set;

    /// <summary>
    /// Creates a new Minimatcher instance.
    /// </summary>
    /// <param name="pattern">The pattern to use for comparision.</param>
    /// <param name="ignoreCase">Ignore casing during comparision.</param>
    /// <param name="allowWindowsPaths">Transform \ to / on both pattern and the input string on comparision.</param>
    /// <exception cref="ArgumentNullException">Raised if the pattern is null.</exception>
    public Minimatch(string pattern, bool ignoreCase, bool allowWindowsPaths)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var trimmedPattern = pattern.Trim();
        this.ignoreCase = ignoreCase;
        this.isWindows = allowWindowsPaths;
        if (allowWindowsPaths)
        {
            trimmedPattern = trimmedPattern.Replace('\\', '/');
        }

        // empty patterns and comments match nothing.
        if (!string.IsNullOrEmpty(trimmedPattern) && trimmedPattern[0] == '#')
        {
            this.comment = true;
            return;
        }

        if (string.IsNullOrEmpty(trimmedPattern))
        {
            this.empty = true;
            return;
        }

        // step 1: figure out negation, etc.
        var negateOffset = 0;

        for (var i = 0; i < trimmedPattern.Length && trimmedPattern[i] == '!'; i++)
        {
            this.negate = !this.negate;
            negateOffset++;
        }

        if (negateOffset > 0)
        {
            trimmedPattern = trimmedPattern[negateOffset..];
        }

        // step 2: expand braces
        var globSet = BraceExpand(trimmedPattern);

        // step 3: now we have a set, so turn each one into a series of path-portion
        // matching patterns.
        // These will be regexps, except in the case of "**", which is
        // set to the GLOBSTAR object for globstar behavior,
        // and will not contain any / characters
        var globParts = globSet.Select(s => SlashSplit.Split(s)).ToList();

        // glob --> regexps
        this.set = globParts.Select(g => g.Select(t => this.Parse(t, false)))
            .Where(g => !g.Contains(null))
            .Select(g => g.Select(t => t.Item1).ToList())
            .ToList();
    }

    /// <summary>
    /// Compare a string to the pattern given on object creation.
    /// </summary>
    /// <param name="input">String to match against the pattern.</param>
    /// <returns>True if the input matches the pattern.</returns>
    public bool IsMatch(string input)
    {
        if (this.comment)
        {
            return false;
        }

        if (this.empty)
        {
            return input.Length == 0;
        }

        // windows: need to use /, not \
        // On other platforms, \ is a valid (albeit bad) filename char.
        if (this.isWindows)
        {
            input = input.Replace("\\", "/");
        }

        // treat the test path as a set of pathparts.
        var segments = SlashSplit.Split(input);

        // just ONE of the pattern sets in this.set needs to match
        // in order for it to be valid.  If negating, then just one
        // match means that we have failed.
        // Either way, return on the first hit.
        foreach (var pattern in this.set)
        {
            if (this.MatchOne(segments, pattern))
            {
                return !this.negate;
            }
        }

        return this.negate;
    }

    private static IEnumerable<string> BraceExpand(string pattern)
    {
        if (!HasBraces.IsMatch(pattern))
        {
            // shortcut. no need to expand.
            return [pattern];
        }

        var escaping = false;
        int i;

        // examples and comments refer to this crazy pattern:
        // a{b,c{d,e},{f,g}h}x{y,z}
        // expected:
        // abxy
        // abxz
        // acdxy
        // acdxz
        // acexy
        // acexz
        // afhxy
        // afhxz
        // aghxy
        // aghxz

        // everything before the first \{ is just a prefix.
        // So, we pluck that off, and work with the rest,
        // and then prepend it to everything we find.
        if (pattern[0] != '{')
        {
            // console.error(pattern)
            string prefix = null;
            for (i = 0; i < pattern.Length; i++)
            {
                var c = pattern[i];

                if (c == '\\')
                {
                    escaping = !escaping;
                }
                else if (c == '{' && !escaping)
                {
                    prefix = pattern[..i];
                    break;
                }
            }

            // actually no sets, all { were escaped.
            if (prefix == null)
            {
                return [pattern];
            }

            return BraceExpand(pattern[i..]).Select(t => prefix + t);
        }

        // now we have something like:
        // {b,c{d,e},{f,g}h}x{y,z}
        // walk through the set, expanding each part, until
        // the set ends.  then, we'll expand the suffix.
        // If the set only has a single member, then'll put the {} back

        // first, handle numeric sets, since they're easier
        var numset = NumericSet.Match(pattern);
        if (numset.Success)
        {
            // console.error("numset", numset[1], numset[2])
            var suf = BraceExpand(pattern[numset.Length..]).ToList();
            int start = int.Parse(numset.Groups[1].Value),
                end = int.Parse(numset.Groups[2].Value),
                inc = start > end ? -1 : 1;
            var retVal = new List<string>();
            for (var w = start; w != end + inc; w += inc)
            {
                // append all the suffixes
                for (var ii = 0; ii < suf.Count; ii++)
                {
                    retVal.Add(w + suf[ii]);
                }
            }

            return retVal;
        }

        // ok, walk through the set
        // We hope, somewhat optimistically, that there
        // will be a } at the end.
        // If the closing brace isn't found, then the pattern is
        // interpreted as braceExpand("\\" + pattern) so that
        // the leading \{ will be interpreted literally.
        var depth = 1;
        var set = new List<string>();
        var member = string.Empty;

        for (i = 1; i < pattern.Length && depth > 0; i++)
        {
            var c = pattern[i];

            if (escaping)
            {
                escaping = false;
                member += "\\" + c;
            }
            else
            {
                switch (c)
                {
                    case '\\':
                        escaping = true;
                        continue;
                    case '{':
                        depth++;
                        member += "{";
                        continue;
                    case '}':
                        depth--;

                        // if this closes the actual set, then we're done
                        if (depth == 0)
                        {
                            set.Add(member);
                            member = string.Empty;

                            // pluck off the close-brace
                            break;
                        }

                        member += c;
                        continue;
                    case ',':
                        if (depth == 1)
                        {
                            set.Add(member);
                            member = string.Empty;
                        }
                        else
                        {
                            member += c;
                        }

                        continue;
                    default:
                        member += c;
                        continue;
                }
            }
        }

        // now we've either finished the set, and the suffix is
        // pattern.substr(i), or we have *not* closed the set,
        // and need to escape the leading brace
        if (depth != 0)
        {
            return BraceExpand("\\" + pattern);
        }

        // ["b", "c{d,e}","{f,g}h"] ->
        //   ["b", "cd", "ce", "fh", "gh"]
        var addBraces = set.Count == 1;

        set = set.SelectMany(BraceExpand).ToList();

        if (addBraces)
        {
            set = set.Select(s => "{" + s + "}").ToList();
        }

        // now attach the suffixes.
        // x{y,z} -> ["xy", "xz"]
        // console.error("set", set)
        // console.error("suffix", pattern.substr(i))
        return BraceExpand(pattern[i..]).SelectMany(s1 => set.Select(s2 => s2 + s1));
    }

    private static string GlobUnescape(string s) => GlobUnescaper.Replace(s, "$1");

    // parse a component of the expanded set.
    // At this point, no pattern may contain "/" in it
    // so we're going to return a 2d array, where each entry is the full
    // pattern, split on '/', and then turned into a regular expression.
    // A regexp is made at the end which joins each array with an
    // escaped /, and another full one which joins each regexp with |.
    //
    // Following the lead of Bash 4.1, note that "**" only has special meaning
    // when it is the *only* thing in a path portion.  Otherwise, any series
    // of * is equivalent to a single *.  Globstar behavior is enabled by
    // default, and can be disabled by setting options.noglobstar.
    private Tuple<ParseItem, bool> Parse(string pattern, bool isSub)
    {
        // shortcuts
        if (pattern == "**")
        {
            return Tuple.Create(GlobStar.Instance, false);
        }

        if (pattern.Length == 0)
        {
            return Tuple.Create(ParseItem.Empty, false);
        }

        var re = string.Empty;
        var hasMagic = this.ignoreCase;
        var escaping = false;
        var inClass = false;

        // ? => one single character
        var patternListStack = new Stack<PatternListEntry>();
        char plType;
        char? stateChar = null;

        int reClassStart = -1, classStart = -1;

        // . and .. never match anything that doesn't start with .,
        // even when options.dot is set.
        var patternStart = pattern[0] == '.' ? string.Empty // anything not (start or / followed by . or .. followed by / or end)
            : "(?!\\.)";

        void ClearStateChar()
        {
            if (stateChar != null)
            {
                // we had some state-tracking character
                // that wasn't consumed by this pass.
                switch (stateChar)
                {
                    case '*':
                        re += Star;
                        hasMagic = true;
                        break;
                    case '?':
                        re += Qmark;
                        hasMagic = true;
                        break;
                    default:
                        re += "\\" + stateChar;
                        break;
                }

                stateChar = null;
            }
        }

        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];

            // skip over any that are escaped.
            if (escaping && ReSpecials.Contains(c))
            {
                re += "\\" + c;
                escaping = false;
                continue;
            }

            switch (c)
            {
                case '/':
                    // completely not allowed, even escaped.
                    // Should already be path-split by now.
                    return null;

                case '\\':
                    ClearStateChar();
                    escaping = true;
                    continue;

                // the various stateChar values
                // for the 'extglob' stuff.
                case '?':
                case '*':
                case '+':
                case '@':
                case '!':
                    // all of those are literals inside a class, except that
                    // the glob [!a] means [^a] in regexp
                    if (inClass)
                    {
                        if (c == '!' && i == classStart + 1)
                        {
                            c = '^';
                        }

                        re += c;
                        continue;
                    }

                    // if we already have a stateChar, then it means
                    // that there was something like ** or +? in there.
                    // Handle the stateChar, then proceed with this one.
                    ClearStateChar();
                    stateChar = c;
                    continue;
                case '(':
                    if (inClass)
                    {
                        re += "(";
                        continue;
                    }

                    if (stateChar == null)
                    {
                        re += "\\(";
                        continue;
                    }

                    plType = stateChar.Value;
                    patternListStack.Push(new PatternListEntry { Type = plType, Start = i - 1, ReStart = re.Length });

                    // negation is (?:(?!js)[^/]*)
                    re += stateChar == '!' ? "(?:(?!" : "(?:";
                    stateChar = null;
                    continue;

                case ')':
                    if (inClass || patternListStack.Count == 0)
                    {
                        re += "\\)";
                        continue;
                    }

                    hasMagic = true;
                    re += ')';
                    plType = patternListStack.Pop().Type;

                    // negation is (?:(?!js)[^/]*)
                    // The others are (?:<pattern>)<type>
                    switch (plType)
                    {
                        case '!':
                            re += "[^/]*?)";
                            break;
                        case '?':
                        case '+':
                        case '*':
                            re += plType;
                            break;
                        case '@':
                            break; // the default anyway
                        default:
                            break;
                    }

                    continue;

                case '|':
                    if (inClass || patternListStack.Count == 0 || escaping)
                    {
                        re += "\\|";
                        escaping = false;
                        continue;
                    }

                    re += "|";
                    continue;

                // these are mostly the same in regexp and glob
                case '[':
                    // swallow any state-tracking char before the [
                    ClearStateChar();

                    if (inClass)
                    {
                        re += "\\" + c;
                        continue;
                    }

                    inClass = true;
                    classStart = i;
                    reClassStart = re.Length;
                    re += c;
                    continue;

                case ']':
                    // a right bracket shall lose its special
                    // meaning and represent itself in
                    // a bracket expression if it occurs
                    // first in the list.  -- POSIX.2 2.8.3.2
                    if (i == classStart + 1 || !inClass)
                    {
                        re += "\\" + c;
                        escaping = false;
                        continue;
                    }

                    // finish up the class.
                    hasMagic = true;
                    inClass = false;
                    re += c;
                    continue;

                default:
                    // swallow any state char that wasn't consumed
                    ClearStateChar();

                    if (escaping)
                    {
                        // no need
                        escaping = false;
                    }
                    else if (ReSpecials.Contains(c) && !(c == '^' && inClass))
                    {
                        re += "\\";
                    }

                    re += c;
                    break;
            }
        }

        // handle the case where we left a class open.
        // "[abc" is valid, equivalent to "\[abc"
        if (inClass)
        {
            // split where the last [ was, and escape it
            // this is a huge pita.  We now have to re-walk
            // the contents of the would-be class to re-translate
            // any characters that were passed through as-is
            var cs = pattern[(classStart + 1)..];
            var sp = this.Parse(cs, true);
            re = re[..reClassStart] + "\\[" + sp.Item1.Source;
            hasMagic = hasMagic || sp.Item2;
        }

        // handle the case where we had a +( thing at the *end*
        // of the pattern.
        // each pattern list stack adds 3 chars, and we need to go through
        // and escape any | chars that were passed through as-is for the regexp.
        // Go through and escape them, taking care not to double-escape any
        // | chars that were already escaped.
        while (patternListStack.Count != 0)
        {
            var pl = patternListStack.Pop();
            var tail = re[(pl.ReStart + 3)..];

            // maybe some even number of \, then maybe 1 \, followed by a |
            tail = EscapeCheck.Replace(tail, m =>
            {
                var escape = m.Groups[2].Value;
                if (string.IsNullOrEmpty(escape))
                {
                    escape = "\\";
                }

                // need to escape all those slashes *again*, without escaping the
                // one that we need for escaping the | character.  As it works out,
                // escaping an even number of slashes can be done by simply repeating
                // it exactly after itself.  That's why this trick works.
                //
                // I am sorry that you have to see this.
                return m.Groups[1].Value + m.Groups[1].Value + escape + "|";
            });

            // console.error("tail=%j\n   %s", tail, tail)
            var t = pl.Type == '*' ? Star
                : pl.Type == '?' ? Qmark
                : "\\" + pl.Type;

            hasMagic = true;
            re = re.Remove(pl.ReStart)
                 + t + "\\("
                 + tail;
        }

        // handle trailing things that only matter at the very end.
        ClearStateChar();
        if (escaping)
        {
            // trailing \\
            re += "\\\\";
        }

        // only need to apply the nodot start if the re starts with
        // something that could conceivably capture a dot
        var addPatternStart = re[0] switch
        {
            '.' or '[' or '(' => true,
            _ => false,
        };

        // if the re is not string.Empty at this point, then we need to make sure
        // it doesn't match against an empty path part.
        // Otherwise a/* will match a/, which it should not.
        if (re.Length != 0 && hasMagic)
        {
            re = "(?=.)" + re;
        }

        if (addPatternStart)
        {
            re = patternStart + re;
        }

        // parsing just a piece of a larger pattern.
        if (isSub)
        {
            return Tuple.Create(ParseItem.Literal(re), hasMagic);
        }

        // skip the regexp for non-magical patterns
        // unescape anything in it, though, so that it'll be
        // an exact match against a file etc.
        if (!hasMagic)
        {
            return Tuple.Create(ParseItem.Literal(GlobUnescape(pattern)), false);
        }

        return new Tuple<ParseItem, bool>(new MagicItem(re, this.ignoreCase), false);
    }

    private bool MatchOne(IList<string> segments, IList<ParseItem> patterns)
    {
        var segmentIndex = 0;
        var patternIndex = 0;
        for (; segmentIndex < segments.Count && patternIndex < patterns.Count; segmentIndex++, patternIndex++)
        {
            var pattern = patterns[patternIndex];
            var file = segments[segmentIndex];

            // should be impossible.
            // some invalid regexp stuff in the set.
            if (pattern == null)
            {
                return false;
            }

            if (pattern is GlobStar)
            {
                // "**"
                // a/**/b/**/c would match the following:
                // a/b/x/y/z/c
                // a/x/y/z/b/c
                // a/b/x/b/x/c
                // a/b/c
                // To do this, take the rest of the pattern after
                // the **, and see if it would match the file remainder.
                // If so, return success.
                // If not, the ** "swallows" a segment, and try again.
                // This is recursively awful.
                //
                // a/**/b/**/c matching a/b/x/y/z/c
                // - a matches a
                // - doublestar
                //   - matchOne(b/x/y/z/c, b/**/c)
                //     - b matches b
                //     - doublestar
                //       - matchOne(x/y/z/c, c) -> no
                //       - matchOne(y/z/c, c) -> no
                //       - matchOne(z/c, c) -> no
                //       - matchOne(c, c) yes, hit
                var innerPatternIndex = patternIndex + 1;
                if (innerPatternIndex == patterns.Count)
                {
                    // a ** at the end will just swallow the rest.
                    // We have found a match.
                    // however, it will not swallow /.x, unless
                    // options.dot is set.
                    // . and .. are *never* matched by **, for explosively
                    // exponential reasons.
                    for (; segmentIndex < segments.Count; segmentIndex++)
                    {
                        if (segments[segmentIndex] == "." || segments[segmentIndex] == ".." ||
                            (!string.IsNullOrEmpty(segments[segmentIndex]) && segments[segmentIndex][0] == '.'))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                // ok, let's see if we can swallow whatever we can.
                for (var i = segmentIndex; i < segments.Count; i++)
                {
                    var swallowee = segments[i];

                    // XXX remove this slice.  Just pass the start index.
                    if (this.MatchOne(segments.Skip(i).ToList(), patterns.Skip(innerPatternIndex).ToList()))
                    {
                        // found a match.
                        return true;
                    }

                    // can't swallow "." or ".." ever.
                    if (swallowee.StartsWith('.') || swallowee == "..")
                    {
                        break;
                    }

                    // ** swallows a segment, and continue.
                }

                return false;
            }

            // something other than **
            // non-magic patterns just have to match exactly
            // patterns with magic have been turned into regexps.
            if (!pattern.Match(file, this.ignoreCase))
            {
                return false;
            }
        }

        // Note: ending in / means that we'll get a final string.Empty
        // at the end of the pattern.  This can only match a
        // corresponding string.Empty at the end of the file.
        // If the file ends in /, then it can only match a
        // a pattern that ends in /, unless the pattern just
        // doesn't have any more for it. But, a/b/ should *not*
        // match "a/b/*", even though string.Empty matches against the
        // [^/]*? pattern, except in partial mode, where it might
        // simply not be reached yet.
        // However, a/b/ should still satisfy a/*

        // now either we fell off the end of the pattern, or we're done.
        if (segmentIndex == segments.Count && patternIndex == patterns.Count)
        {
            // ran out of pattern and filename at the same time.
            // an exact hit!
            return true;
        }

        if (segmentIndex == segments.Count)
        {
            // ran out of file, but still had pattern left.
            // this is ok if we're doing the match as part of
            // a glob fs traversal.
            return false;
        }

        if (patternIndex == patterns.Count)
        {
            // ran out of pattern, still have file left.
            // this is only acceptable if we're on the very last
            // empty segment of a file with a trailing slash.
            // a/* should match a/b/
            var emptyFileEnd = segmentIndex == segments.Count - 1 && segments[segmentIndex].Length == 0;
            return emptyFileEnd;
        }

        throw new InvalidOperationException("This shouldn't happen unless there is a logic bug.");
    }

    private abstract class ParseItem
    {
        public static readonly ParseItem Empty = new LiteralItem(string.Empty);

        public string Source { get; protected init; }

        public static ParseItem Literal(string source) => new LiteralItem(source);

        public abstract bool Match(string input, bool ignoreCase);
    }

    private class LiteralItem : ParseItem
    {
        public LiteralItem(string source) => this.Source = source;

        public override bool Match(string input, bool ignoreCase) => input.Equals(this.Source, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private class MagicItem : ParseItem
    {
        private readonly Lazy<Regex> regex;

        public MagicItem(string source, bool ignoreCase)
        {
            this.Source = source;
            this.regex = new Lazy<Regex>(() =>
            {
                var regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                return new Regex("^" + source + "$", regexOptions);
            });
        }

        public override bool Match(string input, bool ignoreCase) => this.regex.Value.IsMatch(input);
    }

    private class GlobStar : ParseItem
    {
        public static readonly ParseItem Instance = new GlobStar();

        public override bool Match(string input, bool ignoreCase) => throw new NotSupportedException();
    }

    private class PatternListEntry
    {
        public char Type { get; set; }

        public int Start { get; set; }

        public int ReStart { get; set; }
    }
}
