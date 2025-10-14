#nullable disable
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
using System.Diagnostics.CodeAnalysis;
using Microsoft.ComponentDetection.Contracts;

public static class DockerReferenceUtility
{
    // NameTotalLengthMax is the maximum total number of characters in a repository name.
    private const int NameTotalLengthMax = 255;
    private const string DEFAULTDOMAIN = "docker.io";
    private const string LEGACYDEFAULTDOMAIN = "index.docker.io";
    private const string OFFICIALREPOSITORYNAME = "library";

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

    private static DockerReference CreateDockerReference(Reference options)
    {
        return DockerReference.CreateDockerReference(options.Repository, options.Domain, options.Digest, options.Tag);
    }
}
