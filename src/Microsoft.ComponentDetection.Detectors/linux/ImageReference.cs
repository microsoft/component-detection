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
}

/// <summary>
/// Represents a parsed image reference from the scan input, with its type and cleaned reference string.
/// </summary>
internal class ImageReference
{
    private const string OciDirPrefix = "oci-dir:";
    private const string OciArchivePrefix = "oci-archive:";

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
            return new ImageReference
            {
                OriginalInput = input,
                Reference = input[OciDirPrefix.Length..],
                Kind = ImageReferenceKind.OciLayout,
            };
        }

        if (input.StartsWith(OciArchivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new ImageReference
            {
                OriginalInput = input,
                Reference = input[OciArchivePrefix.Length..],
                Kind = ImageReferenceKind.OciArchive,
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
