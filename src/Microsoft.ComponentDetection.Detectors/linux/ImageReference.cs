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

    /// <summary>
    /// Gets the original input string as provided by the user.
    /// </summary>
    public required string OriginalInput { get; init; }

    /// <summary>
    /// Gets the cleaned reference string with any scheme prefix removed.
    /// For Docker images, this is lowercased. For OCI paths, case is preserved.
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>
    /// Gets the kind of image reference.
    /// </summary>
    public required ImageReferenceKind Kind { get; init; }

    /// <summary>
    /// Parses an input image string into an <see cref="ImageReference"/>.
    /// </summary>
    /// <param name="input">The raw image input string.</param>
    /// <returns>A parsed <see cref="ImageReference"/>.</returns>
    public static ImageReference Parse(string input)
    {
        if (input.StartsWith(OciDirPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var path = input[OciDirPrefix.Length..];
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"Input with '{OciDirPrefix}' prefix must include a path.", nameof(input));
            }

            return new ImageReference
            {
                OriginalInput = input,
                Reference = path,
                Kind = ImageReferenceKind.OciLayout,
            };
        }

        if (input.StartsWith(OciArchivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var path = input[OciArchivePrefix.Length..];
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"Input with '{OciArchivePrefix}' prefix must include a path.", nameof(input));
            }

            return new ImageReference
            {
                OriginalInput = input,
                Reference = path,
                Kind = ImageReferenceKind.OciArchive,
            };
        }

        if (input.StartsWith(DockerArchivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var path = input[DockerArchivePrefix.Length..];
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"Input with '{DockerArchivePrefix}' prefix must include a path.", nameof(input));
            }

            return new ImageReference
            {
                OriginalInput = input,
                Reference = path,
                Kind = ImageReferenceKind.DockerArchive,
            };
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
}
