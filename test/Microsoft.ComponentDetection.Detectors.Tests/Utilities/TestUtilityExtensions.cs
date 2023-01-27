namespace Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using System;
using System.Security.Cryptography;

internal static class TestUtilityExtensions
{
    public static string NewRandomVersion() => new Version(
                RandomNumberGenerator.GetInt32(0, 1000),
                RandomNumberGenerator.GetInt32(0, 1000),
                RandomNumberGenerator.GetInt32(0, 1000))
            .ToString();

    public static string GetRandomString() => Guid.NewGuid().ToString("N");
}
