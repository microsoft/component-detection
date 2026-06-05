// transcribed from https://github.com/containers/image/blob/c1a5f92d0ebbf9e0bf187b3353dd400472b388eb/docker/reference/reference.go

// Package reference provides a general type to represent any way of referencing images within the registry.
// Its main purpose is to abstract tags and digests (content-addressable hash).
//
// Grammar
//
// reference                       := name [ ":" tag ] [ "@" digest ]
// name                            := [domain '/'] path-component ['/' path-component]*
// domain                          := domain-component ['.' domain-component]* [':' port-number]
// domain-component                := /([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])/
// port-number                     := /[0-9]+/
// path-component                  := alpha-numeric [separator alpha-numeric]*
// alpha-numeric                   := /[a-z0-9]+/
// separator                       := /[_.]|__|[-]*/
//
// tag                             := /[\w][\w.-]{0,127}/
//
// digest                          := digest-algorithm ":" digest-hex
// digest-algorithm                := digest-algorithm-component [ digest-algorithm-separator digest-algorithm-component ]*
// digest-algorithm-separator      := /[+.-_]/
// digest-algorithm-component      := /[A-Za-z][A-Za-z0-9]*/
// digest-hex                      := /[0-9a-fA-F]{32,}/ ; At least 128 bit digest value
//
// identifier                      := /[a-f0-9]{64}/
// short-identifier                := /[a-f0-9]{6,64}/
namespace Microsoft.ComponentDetection.Common;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

public static class DockerReferenceUtility
{
    // NameTotalLengthMax is the maximum total number of characters in a repository name.
    private const int NameTotalLengthMax = 255;
    private const string DEFAULTDOMAIN = "docker.io";
    private const string LEGACYDEFAULTDOMAIN = "index.docker.io";
    private const string OFFICIALREPOSITORYNAME = "library";

    // Delimiters that only appear in an image reference as part of an unresolved templating
    // token: '$', '{' and '}' cover shell / Helm / Go-template placeholders (e.g. ${VAR},
    // {{ .Values.tag }}). These are recognized templating syntaxes expected in un-rendered manifests,
    // so TryParseImageReference skips them (logging a warning) rather than treating them as invalid.
    // A token wrapped in matching '#' or '!' (handled by DelimiterWrappedTokenRegex) is treated the same way.
    // When no templating token is present, stray invalid characters (e.g. a single '#' or '!') are reported
    // via GetInvalidReferenceCharacters.
    private static readonly char[] TemplateDelimiters = ['$', '{', '}'];

    // Matches token-replacement placeholders that wrap an identifier in double underscores,
    // e.g. __IMAGE_TAG__ or __MCR_ENDPOINT__. Without this they parse as an uppercase repository
    // name and surface as a noisy parse failure instead of being skipped as a templated value.
    private static readonly Regex DoubleUnderscoreTokenRegex = new(@"__\w+__");

    // Matches token-replacement placeholders wrapped in a matching '#' or '!', e.g. #imageTag#,
    // #cs_containerRegistryLoginServerUrl#, or !imageTag!. A string surrounded by the same '#' or
    // '!' delimiter is almost always an unsubstituted template variable (Azure DevOps token
    // replacement and similar), so it is skipped silently instead of surfacing as a misleading
    // docker-reference parse failure. The backreference requires the closing delimiter to match
    // the opening one, so a mismatched stray '#' or '!' is left to GetInvalidReferenceCharacters.
    private static readonly Regex DelimiterWrappedTokenRegex = new(@"([#!])[^#!]+\1");

    // Every character permitted anywhere in a docker reference per the grammar at the top of this
    // file: alphanumerics, the separators '.', '_' and '-', the path separator '/', the tag/port
    // and digest separators ':' and '@', and the digest-algorithm separator '+'. Anything else
    // (e.g. '#', '!') comes from unsubstituted template tokens and is reported as invalid.
    private static readonly SearchValues<char> ValidReferenceChars = SearchValues.Create(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-/:@+");

    /// <summary>
    /// Returns true if the reference contains unresolved variable or templating placeholders,
    /// e.g. <c>${VAR}</c>, <c>{{ .Values.tag }}</c>, <c>__IMAGE_TAG__</c>, <c>#imageTag#</c>, or
    /// <c>!imageTag!</c>.
    /// Such references are not real, resolvable images, so they should be skipped before calling
    /// <see cref="ParseFamiliarName"/> or <see cref="ParseQualifiedName"/> and treated as
    /// unresolved values rather than reported as parse failures.
    /// </summary>
    /// <param name="reference">The image reference string to check.</param>
    /// <returns><c>true</c> if the reference contains variable placeholder characters; otherwise <c>false</c>.</returns>
    public static bool HasUnresolvedVariables(string reference) =>
        reference.IndexOfAny(TemplateDelimiters) >= 0 ||
        DoubleUnderscoreTokenRegex.IsMatch(reference) ||
        DelimiterWrappedTokenRegex.IsMatch(reference);

    /// <summary>
    /// Attempts to parse an image reference string into a <see cref="DockerReference"/>.
    /// Returns <c>null</c> if the reference contains unresolved variables, contains characters that
    /// are not valid in a docker reference, or otherwise cannot be parsed. A warning is logged in
    /// every skip/failure case so that references which are not scanned remain visible in logs.
    /// </summary>
    /// <param name="imageReference">The image reference string to parse.</param>
    /// <param name="logger">Optional logger for recording parse failures.</param>
    /// <returns>A <see cref="DockerReference"/> if parsing succeeds; otherwise <c>null</c>.</returns>
    public static DockerReference? TryParseImageReference(string imageReference, ILogger? logger = null)
    {
        if (HasUnresolvedVariables(imageReference))
        {
            logger?.LogWarning(
                "Skipping image reference '{ImageReference}' because it contains one or more unresolved template tokens or variable placeholders.",
                imageReference);
            return null;
        }

        var invalidCharacters = GetInvalidReferenceCharacters(imageReference);
        if (invalidCharacters.Length > 0)
        {
            logger?.LogWarning(
                "Skipping image reference '{ImageReference}' because it contains character(s) that are not valid in a docker reference: {InvalidCharacters}",
                imageReference,
                invalidCharacters);
            return null;
        }

        try
        {
            return ParseFamiliarName(imageReference);
        }
        catch (DockerReferenceException ex)
        {
            logger?.LogWarning(ex, "Failed to parse image reference '{ImageReference}'.", imageReference);
            return null;
        }
    }

    /// <summary>
    /// Parses an image reference and registers it with the recorder if valid.
    /// Skips references with unresolved variables or that cannot be parsed,
    /// logging a warning in each skipped case so that remaining entries continue to be processed.
    /// </summary>
    /// <param name="imageReference">The image reference string to parse.</param>
    /// <param name="recorder">The component recorder to register the image with.</param>
    /// <param name="logger">Optional logger for recording parse failures.</param>
    public static void TryRegisterImageReference(string imageReference, ISingleFileComponentRecorder recorder, ILogger? logger = null)
    {
        var dockerRef = TryParseImageReference(imageReference, logger);
        TryRegisterImageReference(dockerRef, recorder);
    }

    /// <summary>
    /// Registers a pre-parsed <see cref="DockerReference"/> with the recorder if non-null.
    /// </summary>
    /// <param name="dockerReference">The parsed docker reference, or <c>null</c> to skip.</param>
    /// <param name="recorder">The component recorder to register the image with.</param>
    public static void TryRegisterImageReference(DockerReference? dockerReference, ISingleFileComponentRecorder recorder)
    {
        if (dockerReference != null)
        {
            recorder.RegisterUsage(new DetectedComponent(dockerReference.ToTypedDockerReferenceComponent()));
        }
    }

    public static DockerReference ParseQualifiedName(string qualifiedName)
    {
        var regexp = DockerRegex.ReferenceRegexp;
        if (!regexp.IsMatch(qualifiedName))
        {
            if (string.IsNullOrWhiteSpace(qualifiedName))
            {
                throw new ReferenceNameEmptyException(qualifiedName);
            }

            if (regexp.IsMatch(qualifiedName.ToLower()))
            {
                throw new ReferenceNameContainsUppercaseException(qualifiedName);
            }

            throw new ReferenceInvalidFormatException(qualifiedName);
        }

        var matches = regexp.Match(qualifiedName).Groups;

        var name = matches[1].Value;
        if (name.Length > NameTotalLengthMax)
        {
            throw new ReferenceNameTooLongException(name);
        }

        var reference = new Reference();

        var nameMatch = DockerRegex.AnchoredNameRegexp.Match(name).Groups;
        if (nameMatch.Count == 3)
        {
            reference.Domain = nameMatch[1].Value;
            reference.Repository = nameMatch[2].Value;
        }
        else
        {
            reference.Domain = string.Empty;
            reference.Repository = matches[1].Value;
        }

        reference.Tag = matches[2].Value;

        if (matches.Count > 3 && !string.IsNullOrEmpty(matches[3].Value))
        {
            DigestUtility.CheckDigest(matches[3].Value, true);
            reference.Digest = matches[3].Value;
        }

        return CreateDockerReference(reference);
    }

    public static (string Domain, string Remainder) SplitDockerDomain(string name)
    {
        string domain;
        string remainder;

        var indexOfSlash = name.IndexOf('/');
        if (indexOfSlash == -1 || !(
                name.LastIndexOf('.', indexOfSlash) != -1 ||
                name.LastIndexOf(':', indexOfSlash) != -1 ||
                name.StartsWith("localhost/")))
        {
            domain = DEFAULTDOMAIN;
            remainder = name;
        }
        else
        {
            domain = name[..indexOfSlash];
            remainder = name[(indexOfSlash + 1)..];
        }

        if (domain == LEGACYDEFAULTDOMAIN)
        {
            domain = DEFAULTDOMAIN;
        }

        if (domain == DEFAULTDOMAIN && indexOfSlash == -1)
        {
            remainder = $"{OFFICIALREPOSITORYNAME}/{remainder}";
        }

        return (domain, remainder);
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Explicitly checks for character case.")]
    public static DockerReference ParseFamiliarName(string name)
    {
        if (DockerRegex.AnchoredIdentifierRegexp.IsMatch(name))
        {
            throw new ReferenceNameNotCanonicalException(name);
        }

        (var domain, var remainder) = SplitDockerDomain(name);

        string remoteName;
        var tagSeparatorIndex = remainder.IndexOf(':');
        if (tagSeparatorIndex > -1)
        {
            remoteName = remainder[..tagSeparatorIndex];
        }
        else
        {
            remoteName = remainder;
        }

        if (!string.Equals(remoteName.ToLowerInvariant(), remoteName, StringComparison.InvariantCulture))
        {
            throw new ReferenceNameContainsUppercaseException(name);
        }

        return ParseQualifiedName($"{domain}/{remainder}");
    }

    public static DockerReference ParseAll(string name)
    {
        if (DockerRegex.AnchoredIdentifierRegexp.IsMatch(name))
        {
            return CreateDockerReference(new Reference { Digest = $"sha256:{name}" });
        }

        if (DigestUtility.CheckDigest(name, false))
        {
            return CreateDockerReference(new Reference { Digest = name });
        }

        return ParseFamiliarName(name);
    }

    /// <summary>
    /// Returns the distinct characters in <paramref name="reference"/> that are not valid in any
    /// part of a docker reference (domain, repository, tag, or digest) as a comma-separated string,
    /// or an empty string when every character is valid. Characters such as <c>#</c> and <c>!</c>
    /// commonly appear in unsubstituted template tokens and otherwise surface as misleading
    /// "must be lowercase" or "invalid reference format" parse errors.
    /// </summary>
    /// <param name="reference">The image reference string to inspect.</param>
    /// <returns>A comma-separated list of invalid characters, or an empty string if there are none.</returns>
    private static string GetInvalidReferenceCharacters(string reference)
    {
        // Vectorized happy-path check: the overwhelmingly common case is an all-valid reference,
        // for which this returns without allocating. Only gather the offending characters when
        // at least one is present.
        var span = reference.AsSpan();
        if (!span.ContainsAnyExcept(ValidReferenceChars))
        {
            return string.Empty;
        }

        SortedSet<char> invalid = [];
        foreach (var c in span)
        {
            if (!ValidReferenceChars.Contains(c))
            {
                invalid.Add(c);
            }
        }

        var invalidStrings = new List<string>(invalid.Count);
        foreach (var c in invalid)
        {
            invalidStrings.Add($"'{c}'");
        }

        return string.Join(", ", invalidStrings);
    }

    private static DockerReference CreateDockerReference(Reference options)
    {
        return DockerReference.CreateDockerReference(options.Repository, options.Domain, options.Digest, options.Tag);
    }
}
