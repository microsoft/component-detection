using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.ComponentDetection.Common
{
    public class DockerRegex
    {
        /**
 * expression defines a full expression, where each regular expression must follow the previous.
 */
        public static Regex Expression(params Regex[] regexps)
        {
            return new Regex(string.Join(string.Empty, regexps.Select(re => re.ToString())));
        }

        /**
         * group wraps the regexp in a non-capturing group.
         */
        public static Regex Group(params Regex[] regexps)
        {
            return new Regex($"(?:{Expression(regexps).ToString()})");
        }

        /**
         * repeated wraps the regexp in a non-capturing group to get one or more matches.
         */
        public static Regex Optional(params Regex[] regexps)
        {
            return new Regex($"{Group(regexps).ToString()}?");
        }

        /**
         * repeated wraps the regexp in a non-capturing group to get one or more matches.
         */
        public static Regex Repeated(params Regex[] regexps)
        {
            return new Regex($"{Group(regexps).ToString()}+");
        }

        /**
         * anchored anchors the regular expression by adding start and end delimiters.
         */
        public static Regex Anchored(params Regex[] regexps)
        {
            return new Regex($"^{Expression(regexps).ToString()}$");
        }

        /**
         * capture wraps the expression in a capturing group.
         */
        public static Regex Capture(params Regex[] regexps)
        {
            return new Regex($"({Expression(regexps).ToString()})");
        }

        public static Regex AlphaNumericRegexp = new Regex("[a-z0-9]+");
        public static Regex SeparatorRegexp = new Regex("(?:[._]|__|[-]*)");

        public static Regex DomainComponentRegexp = new Regex("(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])");
        public static Regex TagRegexp = new Regex(@"[\w][\w.-]{0,127}");
        public static Regex DigestRegexp = new Regex("[a-zA-Z][a-zA-Z0-9]*(?:[-_+.][a-zA-Z][a-zA-Z0-9]*)*[:][a-fA-F0-9]{32,}");
        public static Regex IdentifierRegexp = new Regex("[a-f0-9]{64}");

        public static Regex NameComponentRegexp = Expression(
             AlphaNumericRegexp,
             Optional(Repeated(SeparatorRegexp, AlphaNumericRegexp)));

        public static Regex DomainRegexp = Expression(
            DomainComponentRegexp,
            Optional(
                Repeated(
                    new Regex(@"\."), 
                    DomainComponentRegexp)),
            Optional(
                new Regex(":"), 
                new Regex("[0-9]+")));

        public static Regex AnchoredDigestRegexp = Anchored(DigestRegexp);

        public static Regex NameRegexp = Expression(
            Optional(
                DomainRegexp,
                new Regex(@"\/")),
            NameComponentRegexp,
            Optional(
                Repeated(
                    new Regex(@"\/"),
                    NameComponentRegexp)));

        public static Regex AnchoredNameRegexp = Anchored(
            Optional(
                Capture(DomainRegexp),
                new Regex(@"\/")),
            Capture(
                NameComponentRegexp,
                Optional(
                    Repeated(
                        new Regex(@"\/"),
                        NameComponentRegexp))));

        public static Regex ReferenceRegexp = Anchored(
            Capture(NameRegexp),
            Optional(new Regex(":"), Capture(TagRegexp)),
            Optional(new Regex("@"), Capture(DigestRegexp)));

        public static Regex AnchoredIdentifierRegexp = Anchored(IdentifierRegexp);
    }
}