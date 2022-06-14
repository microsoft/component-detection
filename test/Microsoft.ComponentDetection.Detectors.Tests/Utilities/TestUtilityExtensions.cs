using System;
using System.Security.Cryptography;

namespace Microsoft.ComponentDetection.Detectors.Tests.Utilities
{
    internal static class TestUtilityExtensions
    {
        public static string NewRandomVersion()
        {
            return new Version(
                RandomNumberGenerator.GetInt32(0, 1000),
                RandomNumberGenerator.GetInt32(0, 1000),
                RandomNumberGenerator.GetInt32(0, 1000))
                .ToString();
        }

        public static string GetRandomString()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
