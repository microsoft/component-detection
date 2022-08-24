using System.IO;
using System.Text;
using Microsoft.ComponentDetection.Contracts;
using Moq;
using Microsoft.ComponentDetection.TestsUtilities;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    public static class YarnTestUtilities
    {
        public static string GetWellFormedEmptyYarnV1LockFile()
        {
            var builder = new StringBuilder();

            builder.AppendLine("# THIS IS A YARNFILE");
            builder.AppendLine("# yarn lockfile v1");
            builder.AppendLine();

            return builder.ToString();
        }

        public static string GetWellFormedEmptyYarnV2LockFile()
        {
            var builder = new StringBuilder();

            builder.AppendLine("# THIS IS A YARNFILE");
            builder.AppendLine();
            builder.AppendLine("__metadata:");
            builder.AppendLine("  version: 4");
            builder.AppendLine("  cacheKey: 7");
            builder.AppendLine();

            return builder.ToString();
        }

        public static IComponentStream GetMockedYarnLockStream(string lockFileName, string content)
        {
            var packageLockMock = new Mock<IComponentStream>();
            packageLockMock.SetupGet(x => x.Stream).Returns(content.ToString().ToStream());
            packageLockMock.SetupGet(x => x.Pattern).Returns(lockFileName);
            packageLockMock.SetupGet(x => x.Location).Returns(Path.Combine(Path.GetTempPath(), lockFileName));

            return packageLockMock.Object;
        }
    }
}
