using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class NuGetNuspecUtilitiesTests
    {
        [TestMethod]
        public async Task GetNuspecBytes_FailsOnEmptyStream()
        {
            using var stream = new MemoryStream();

            async Task ShouldThrow() => await NuGetNuspecUtilities.GetNuspecBytesAsync(stream);

            await Assert.ThrowsExceptionAsync<ArgumentException>(ShouldThrow);

            // The position should always be reset to 0
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public async Task GetNuspecBytes_FailsOnTooSmallStream()
        {
            using var stream = new MemoryStream();

            for (var i = 0; i < NuGetNuspecUtilities.MinimumLengthForZipArchive - 1; i++)
            {
                stream.WriteByte(0);
            }

            stream.Seek(0, SeekOrigin.Begin);

            async Task ShouldThrow() => await NuGetNuspecUtilities.GetNuspecBytesAsync(stream);

            await Assert.ThrowsExceptionAsync<ArgumentException>(ShouldThrow);

            // The position should always be reset to 0
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public async Task GetNuspecBytes_FailsIfNuspecNotPresent()
        {
            using var stream = new MemoryStream();

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                archive.CreateEntry("test.txt");
            }

            stream.Seek(0, SeekOrigin.Begin);

            async Task ShouldThrow() => await NuGetNuspecUtilities.GetNuspecBytesAsync(stream);

            // No Nuspec File is in the archive
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(ShouldThrow);

            // The position should always be reset to 0
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public async Task GetNuspecBytes_ReadsNuspecBytes()
        {
            byte[] randomBytes = { 0xDE, 0xAD, 0xC0, 0xDE };

            using var stream = new MemoryStream();

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("test.nuspec");

                using var entryStream = entry.Open();

                await entryStream.WriteAsync(randomBytes, 0, randomBytes.Length);
            }

            stream.Seek(0, SeekOrigin.Begin);

            var bytes = await NuGetNuspecUtilities.GetNuspecBytesAsync(stream);

            Assert.AreEqual(randomBytes.Length, bytes.Length);

            for (var i = 0; i < randomBytes.Length; i++)
            {
                Assert.AreEqual(randomBytes[i], bytes[i]);
            }

            // The position should always be reset to 0
            Assert.AreEqual(0, stream.Position);
        }
    }
}
