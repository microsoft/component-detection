using System;

#pragma warning disable SA1402
namespace Microsoft.ComponentDetection.Common
{
    public class DockerReferenceException : Exception
    {
        public DockerReferenceException(string reference, string exceptionErrorMessage)
            : base($"Error while parsing docker reference {reference} : {exceptionErrorMessage}")
        {
        }
    }

    // ReferenceInvalidFormat represents an error while trying to parse a string as a reference.
    public class ReferenceInvalidFormatException : DockerReferenceException
    {
        private const string ErrorMessage = "invalid reference format";

        public ReferenceInvalidFormatException(string reference) 
            : base(reference, ErrorMessage)
        {
        }
    }

    // TagInvalidFormat represents an error while trying to parse a string as a tag.
    public class ReferenceTagInvalidFormatException : DockerReferenceException
    {
        private const string ErrorMessage = "invalid tag format";

        public ReferenceTagInvalidFormatException(string reference)
            : base(reference, ErrorMessage)
        {
        }
    }

    // DigestInvalidFormat represents an error while trying to parse a string as a tag.
    public class ReferenceDigestInvalidFormatException : DockerReferenceException
    {
        private const string ErrorMessage = "invalid digest format";

        public ReferenceDigestInvalidFormatException(string reference)
            : base(reference, ErrorMessage)
        {
        }
    }

    // NameContainsUppercase is returned for invalid repository names that contain uppercase characters.
    public class ReferenceNameContainsUppercaseException : DockerReferenceException
    {
        private const string ErrorMessage = "repository name must be lowercase";

        public ReferenceNameContainsUppercaseException(string reference)
            : base(reference, ErrorMessage)
        {
        }
    }

    // NameEmpty is returned for empty, invalid repository names.
    public class ReferenceNameEmptyException : DockerReferenceException
    {
        private const string ErrorMessage = "repository name must have at least one component";

        public ReferenceNameEmptyException(string reference)
            : base(reference, ErrorMessage)
        {
        }
    }

    // ErrNameTooLong is returned when a repository name is longer than NameTotalLengthMax.
    public class ReferenceNameTooLongException : DockerReferenceException
    {
        private const string ErrorMessage = "repository name must not be more than 255 characters";

        public ReferenceNameTooLongException(string reference)
            : base(reference, ErrorMessage)
        {
        }
    }

    // ErrNameNotCanonical is returned when a name is not canonical.
    public class ReferenceNameNotCanonicalException : DockerReferenceException
    {
        private const string ErrorMessage = "repository name must be canonical";

        public ReferenceNameNotCanonicalException(string reference)
            : base(reference, ErrorMessage)
        {
        }
    }

    public class InvalidDigestFormatError : DockerReferenceException
    {
        private const string ErrorMessage = "invalid digest format";

        public InvalidDigestFormatError(string reference)
            : base(reference, ErrorMessage)
        {
        }
    }

    public class UnsupportedAlgorithmError : DockerReferenceException
    {
        private const string ErrorMessage = "unsupported digest algorithm";

        public UnsupportedAlgorithmError(string reference)
            : base(reference, ErrorMessage)
        {
        }
    }

    public class InvalidDigestLengthError : DockerReferenceException
    {
        private const string ErrorMessage = "invalid checksum digest length";

        public InvalidDigestLengthError(string reference)
            : base(reference, ErrorMessage)
        {
        }
    }
}