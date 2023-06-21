namespace Microsoft.ComponentDetection.Common;

using System.Linq;
using System.Text.RegularExpressions;

public static class DockerRegex
{
    public static readonly Regex AlphaNumericRegexp = new Regex("[a-z0-9]+");
    public static readonly Regex SeparatorRegexp = new Regex("(?:[._]|__|[-]*)");

    public static readonly Regex DomainComponentRegexp = new Regex("(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])");
    public static readonly Regex TagRegexp = new Regex(@"[\w][\w.-]{0,127}");
    public static readonly Regex DigestRegexp = new Regex("[a-zA-Z][a-zA-Z0-9]*(?:[-_+.][a-zA-Z][a-zA-Z0-9]*)*[:][a-fA-F0-9]{32,}");
    public static readonly Regex IdentifierRegexp = new Regex("[a-f0-9]{64}");

    public static readonly Regex NameComponentRegexp = Expression(
        AlphaNumericRegexp,
        Optional(Repeated(SeparatorRegexp, AlphaNumericRegexp)));

    public static readonly Regex DomainRegexp = Expression(
        DomainComponentRegexp,
        Optional(
            Repeated(
                new Regex(@"\."),
                DomainComponentRegexp)),
        Optional(
            new Regex(":"),
            new Regex("[0-9]+")));

    public static readonly Regex AnchoredDigestRegexp = Anchored(DigestRegexp);

    public static readonly Regex NameRegexp = Expression(
        Optional(
            DomainRegexp,
            new Regex(@"\/")),
        NameComponentRegexp,
        Optional(
            Repeated(
                new Regex(@"\/"),
                NameComponentRegexp)));

    public static readonly Regex AnchoredNameRegexp = Anchored(
        Optional(
            Capture(DomainRegexp),
            new Regex(@"\/")),
        Capture(
            NameComponentRegexp,
            Optional(
                Repeated(
                    new Regex(@"\/"),
                    NameComponentRegexp))));

    public static readonly Regex ReferenceRegexp = Anchored(
        Capture(NameRegexp),
        Optional(new Regex(":"), Capture(TagRegexp)),
        Optional(new Regex("@"), Capture(DigestRegexp)));

    public static readonly Regex AnchoredIdentifierRegexp = Anchored(IdentifierRegexp);

    /// <summary>
    /// expression defines a full expression, where each regular expression must follow the previous.
    /// </summary>
    /// <param name="regexps">list of Regular expressions.</param>
    /// <returns> full Regex expression <see cref="Regex"/> from the given list. </returns>
    public static Regex Expression(params Regex[] regexps)
    {
        return new Regex(string.Join(string.Empty, regexps.Select(re => re.ToString())));
    }

    /// <summary>
    /// group wraps the regexp in a non-capturing group.
    /// </summary>
    /// <param name="regexps">list of Regular expressions.</param>
    /// <returns> <see cref="Regex"/> of the non-capturing group. </returns>
    public static Regex Group(params Regex[] regexps)
    {
        return new Regex($"(?:{Expression(regexps)})");
    }

    /// <summary>
    /// repeated wraps the regexp in a non-capturing group to get one or more matches.
    /// </summary>
    /// <param name="regexps">list of Regular expressions.</param>
    /// <returns> The wrapped <see cref="Regex"/>. </returns>
    public static Regex Optional(params Regex[] regexps)
    {
        return new Regex($"{Group(regexps)}?");
    }

    /// <summary>
    /// repeated wraps the regexp in a non-capturing group to get one or more matches.
    /// </summary>
    /// <param name="regexps">list of Regular expressions.</param>
    /// <returns> The wrapped <see cref="Regex"/>. </returns>
    public static Regex Repeated(params Regex[] regexps)
    {
        return new Regex($"{Group(regexps)}+");
    }

    /// <summary>
    /// anchored anchors the regular expression by adding start and end delimiters.
    /// </summary>
    /// <param name="regexps">list of Regular expressions.</param>
    /// <returns> The anchored <see cref="Regex"/>. </returns>
    public static Regex Anchored(params Regex[] regexps)
    {
        return new Regex($"^{Expression(regexps)}$");
    }

    /// <summary>
    /// capture wraps the expression in a capturing group.
    /// </summary>
    /// <param name="regexps">list of Regular expressions.</param>
    /// <returns> The captured <see cref="Regex"/>. </returns>
    public static Regex Capture(params Regex[] regexps)
    {
        return new Regex($"({Expression(regexps)})");
    }
}
