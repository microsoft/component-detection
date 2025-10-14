#nullable disable
namespace Microsoft.ComponentDetection.Common.DockerReference;

using System.Collections.Generic;

public static class DigestUtility
{
    private static readonly Dictionary<string, int> AlgorithmsSizes = new Dictionary<string, int>()
    {
        { "sha256", 32 },
        { "sha384", 48 },
        { "sha512", 64 },
    };

    public static bool CheckDigest(string digest, bool throwError = true)
    {
        var indexOfColon = digest.IndexOf(':');
        if (indexOfColon < 0 ||
            indexOfColon + 1 == digest.Length ||
            !DockerRegex.AnchoredDigestRegexp.IsMatch(digest))
        {
            if (throwError)
            {
                throw new InvalidDigestFormatError(digest);
            }

            return false;
        }

        var algorithm = digest[..indexOfColon];

        if (!AlgorithmsSizes.ContainsKey(algorithm))
        {
            if (throwError)
            {
                throw new UnsupportedAlgorithmError(digest);
            }

            return false;
        }

        if (AlgorithmsSizes[algorithm] * 2 != (digest.Length - indexOfColon - 1))
        {
            if (throwError)
            {
                throw new InvalidDigestLengthError(digest);
            }

            return false;
        }

        return true;
    }
}
