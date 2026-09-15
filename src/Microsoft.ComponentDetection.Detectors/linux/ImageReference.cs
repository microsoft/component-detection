namespace Microsoft.ComponentDetection.Detectors.Linux;

using System;

/// <summary>
/// Specifies the type of image reference.
/// </summary>
internal enum ImageReferenceKind
{
    /// <summary>
    /// A Docker image reference (e.g., "node:latest", "sha256:abc123").
    /// </summary>
    DockerImage,

    /// <summary>
    /// An OCI Image Layout directory on disk (e.g., "oci-dir:/path/to/image").
    /// </summary>
    OciLayout,

    /// <summary>
    /// An OCI archive (tarball) file on disk (e.g., "oci-archive:/path/to/image.tar").
    /// </summary>
    OciArchive,

    /// <summary>
    /// A Docker archive (tarball) file on disk created by "docker save" (e.g., "docker-archive:/path/to/image.tar").
    /// </summary>
    DockerArchive,
}

/// <summary>
/// Represents a parsed image reference from the scan input, with its type and cleaned reference string.
/// </summary>
internal class ImageReference
{
    private const string OciDirPrefix = "oci-dir:";
    private const string OciArchivePrefix = "oci-archive:";
    private const string DockerArchivePrefix = "docker-archive:";
    private const string PlatformParameter = "?platform=";

    /// <summary>
    /// Gets the original input string as provided by the user.
    /// </summary>
    public required string OriginalInput { get; init; }

    /// <summary>
    /// Gets the cleaned reference string with any scheme prefix removed.
    /// For Docker images, this is lowercased. For file paths, case is preserved.
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>
    /// Gets the kind of image reference.
    /// </summary>
    public required ImageReferenceKind Kind { get; init; }

    /// <summary>
    /// Gets the optional platform to select when scanning a local image source.
    /// </summary>
    public string? Platform { get; init; }

    /// <summary>
    /// Parses an input image string into an <see cref="ImageReference"/>.
    /// </summary>
    /// <param name="input">The raw image input string.</param>
    /// <returns>A parsed <see cref="ImageReference"/>.</returns>
    public static ImageReference Parse(string input)
    {
        if (input.StartsWith(OciDirPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ParseLocalReference(input, OciDirPrefix, ImageReferenceKind.OciLayout);
        }

        if (input.StartsWith(OciArchivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ParseLocalReference(input, OciArchivePrefix, ImageReferenceKind.OciArchive);
        }

        if (input.StartsWith(DockerArchivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ParseLocalReference(input, DockerArchivePrefix, ImageReferenceKind.DockerArchive);
        }

        if (input.Contains(PlatformParameter, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Platform selection is supported only for OCI layouts, OCI archives, and Docker archives.",
                nameof(input));
        }

#pragma warning disable CA1308
        return new ImageReference
        {
            OriginalInput = input,
            Reference = input.ToLowerInvariant(),
            Kind = ImageReferenceKind.DockerImage,
        };
#pragma warning restore CA1308
    }

    private static ImageReference ParseLocalReference(
        string input,
        string prefix,
        ImageReferenceKind kind)
    {
        var pathAndParameters = input[prefix.Length..];
        var platformParameterIndex = pathAndParameters.LastIndexOf(
            PlatformParameter,
            StringComparison.OrdinalIgnoreCase);

        var path = platformParameterIndex >= 0
            ? pathAndParameters[..platformParameterIndex]
            : pathAndParameters;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"Input with '{prefix}' prefix must include a path.", nameof(input));
        }

        string? platform = null;
        if (platformParameterIndex >= 0)
        {
            platform = pathAndParameters[(platformParameterIndex + PlatformParameter.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(platform))
            {
                throw new ArgumentException("The platform parameter must include a value.", nameof(input));
            }
        }

        return new ImageReference
        {
            OriginalInput = input,
            Reference = path,
            Kind = kind,
            Platform = platform,
        };
    }
}
