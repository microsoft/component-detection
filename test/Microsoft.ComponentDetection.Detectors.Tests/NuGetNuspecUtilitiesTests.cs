using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class NuGetNuspecUtilitiesTests
    {
        [TestMethod]
        public void GetNuspecBytes_FailsOnEmptyStream()
        {
            using var stream = new MemoryStream();

            void ShouldThrow() => NuGetNuspecUtilities.GetNuspecDataFromNuspecStream(stream);

            Assert.ThrowsException<ArgumentException>(ShouldThrow);

            // The position should always be reset to 0
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public void GetNuspecBytes_FailsOnTooSmallStream()
        {
            using var stream = new MemoryStream();

            for (int i = 0; i < NuGetNuspecUtilities.MinimumLengthForZipArchive - 1; i++)
            {
                stream.WriteByte(0);
            }

            stream.Seek(0, SeekOrigin.Begin);

            var getFileMock = new Mock<IComponentStream>();
            getFileMock.SetupGet(x => x.Stream).Returns(stream);
            getFileMock.SetupGet(x => x.Pattern).Returns(default(string));
            getFileMock.SetupGet(x => x.Location).Returns("test.nupkg");

            void ShouldThrow() => NuGetNuspecUtilities.GetNuGetPackageDataFromNupkg(getFileMock.Object);

            Assert.ThrowsException<ArgumentException>(ShouldThrow);

            // The position should always be reset to 0
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public void GetNuspecBytes_FailsIfNuspecNotPresent()
        {
            using var stream = new MemoryStream();

            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                archive.CreateEntry("test.txt");
            }

            stream.Seek(0, SeekOrigin.Begin);

            var getFileMock = new Mock<IComponentStream>();
            getFileMock.SetupGet(x => x.Stream).Returns(stream);
            getFileMock.SetupGet(x => x.Pattern).Returns(default(string));
            getFileMock.SetupGet(x => x.Location).Returns("test.nupkg");

            void ShouldThrow() => NuGetNuspecUtilities.GetNuGetPackageDataFromNupkg(getFileMock.Object);

            // No Nuspec File is in the archive
            Assert.ThrowsException<FileNotFoundException>(ShouldThrow);

            // The position should always be reset to 0
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public async Task GetNuspecBytes_ReadsNuspecBytes()
        {
            var nuspecContent = NugetTestUtilities.GetValidNuspec("Test", "1.2.3", new[] { "Test1", "Test2" });
            var bytes = System.Text.Encoding.UTF8.GetBytes(nuspecContent);

            using var stream = new MemoryStream();

            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("test.nuspec");

                using (var entryStream = entry.Open())
                {
                    await entryStream.WriteAsync(bytes, 0, bytes.Length);
                }

                var libEntry = archive.CreateEntry(@"lib\net48\test.dll");
            }

            stream.Seek(0, SeekOrigin.Begin);

            var getFileMock = new Mock<IComponentStream>();
            getFileMock.SetupGet(x => x.Stream).Returns(stream);
            getFileMock.SetupGet(x => x.Pattern).Returns(default(string));
            getFileMock.SetupGet(x => x.Location).Returns("test.nupkg");

            var packageData = NuGetNuspecUtilities.GetNuGetPackageDataFromNupkg(getFileMock.Object);

            Assert.AreEqual("Test", packageData.name);
            Assert.AreEqual("1.2.3", packageData.version);
            Assert.AreEqual(2, packageData.authors.Length);
            Assert.AreEqual("Test1", packageData.authors[0]);
            Assert.AreEqual("Test2", packageData.authors[1]);
            bool set = false;
            foreach (var framework in packageData.targetFrameworks)
            {
                Assert.IsFalse(set, "We got more target frameworks than anticipated");
                Assert.AreEqual(".NETFramework,Version=v4.8", framework);
                set = true;
            }

            Assert.IsTrue(set, "We didn't get the target framework");

            // The position should always be reset to 0
            Assert.AreEqual(0, stream.Position);
        }
    }
}
