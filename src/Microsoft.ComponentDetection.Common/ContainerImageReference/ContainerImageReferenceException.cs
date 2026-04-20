#pragma warning disable SA1402
namespace Microsoft.ComponentDetection.Common;

using System;

internal class ContainerImageReferenceException : Exception
{
    public ContainerImageReferenceException(string reference, string exceptionErrorMessage)
        : base($"Error while parsing container image reference {reference} : {exceptionErrorMessage}")
    {
    }

    public ContainerImageReferenceException()
    {
    }

    public ContainerImageReferenceException(string message)
        : base(message)
    {
    }

    public ContainerImageReferenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

// ReferenceInvalidFormat represents an error while trying to parse a string as a reference.
internal class ReferenceInvalidFormatException : ContainerImageReferenceException
{
    private const string ErrorMessage = "invalid reference format";

    public ReferenceInvalidFormatException(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public ReferenceInvalidFormatException()
    {
    }

    public ReferenceInvalidFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

// TagInvalidFormat represents an error while trying to parse a string as a tag.
internal class ReferenceTagInvalidFormatException : ContainerImageReferenceException
{
    private const string ErrorMessage = "invalid tag format";

    public ReferenceTagInvalidFormatException(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public ReferenceTagInvalidFormatException()
    {
    }

    public ReferenceTagInvalidFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

// DigestInvalidFormat represents an error while trying to parse a string as a tag.
internal class ReferenceDigestInvalidFormatException : ContainerImageReferenceException
{
    private const string ErrorMessage = "invalid digest format";

    public ReferenceDigestInvalidFormatException(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public ReferenceDigestInvalidFormatException()
    {
    }

    public ReferenceDigestInvalidFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

// NameContainsUppercase is returned for invalid repository names that contain uppercase characters.
internal class ReferenceNameContainsUppercaseException : ContainerImageReferenceException
{
    private const string ErrorMessage = "repository name must be lowercase";

    public ReferenceNameContainsUppercaseException(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public ReferenceNameContainsUppercaseException()
    {
    }

    public ReferenceNameContainsUppercaseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

// NameEmpty is returned for empty, invalid repository names.
internal class ReferenceNameEmptyException : ContainerImageReferenceException
{
    private const string ErrorMessage = "repository name must have at least one component";

    public ReferenceNameEmptyException(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public ReferenceNameEmptyException()
    {
    }

    public ReferenceNameEmptyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

// ErrNameTooLong is returned when a repository name is longer than NameTotalLengthMax.
internal class ReferenceNameTooLongException : ContainerImageReferenceException
{
    private const string ErrorMessage = "repository name must not be more than 255 characters";

    public ReferenceNameTooLongException(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public ReferenceNameTooLongException()
    {
    }

    public ReferenceNameTooLongException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

// ErrNameNotCanonical is returned when a name is not canonical.
internal class ReferenceNameNotCanonicalException : ContainerImageReferenceException
{
    private const string ErrorMessage = "repository name must be canonical";

    public ReferenceNameNotCanonicalException(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public ReferenceNameNotCanonicalException()
    {
    }

    public ReferenceNameNotCanonicalException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal class InvalidDigestFormatError : ContainerImageReferenceException
{
    private const string ErrorMessage = "invalid digest format";

    public InvalidDigestFormatError(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public InvalidDigestFormatError()
    {
    }

    public InvalidDigestFormatError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal class UnsupportedAlgorithmError : ContainerImageReferenceException
{
    private const string ErrorMessage = "unsupported digest algorithm";

    public UnsupportedAlgorithmError(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public UnsupportedAlgorithmError()
    {
    }

    public UnsupportedAlgorithmError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal class InvalidDigestLengthError : ContainerImageReferenceException
{
    private const string ErrorMessage = "invalid checksum digest length";

    public InvalidDigestLengthError(string reference)
        : base(reference, ErrorMessage)
    {
    }

    public InvalidDigestLengthError()
    {
    }

    public InvalidDigestLengthError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
