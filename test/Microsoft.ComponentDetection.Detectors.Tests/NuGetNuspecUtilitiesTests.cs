#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NuGetNuspecUtilitiesTests
{
    [TestMethod]
    public async Task GetNuspecBytes_FailsOnEmptyStreamAsync()
    {
        using var stream = new MemoryStream();

        var action = async () => await NuGetNuspecUtilities.GetNuspecBytesAsync(stream);
        await action.Should().ThrowAsync<ArgumentException>();

        // The position should always be reset to 0
        stream.Position.Should().Be(0);
    }

    [TestMethod]
    public async Task GetNuspecBytes_FailsOnTooSmallStreamAsync()
    {
        using var stream = new MemoryStream();

        for (var i = 0; i < NuGetNuspecUtilities.MinimumLengthForZipArchive - 1; i++)
        {
            stream.WriteByte(0);
        }

        stream.Seek(0, SeekOrigin.Begin);

        var action = async () => await NuGetNuspecUtilities.GetNuspecBytesAsync(stream);
        await action.Should().ThrowAsync<ArgumentException>();

        // The position should always be reset to 0
        stream.Position.Should().Be(0);
    }

    [TestMethod]
    public async Task GetNuspecBytes_FailsIfNuspecNotPresentAsync()
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            archive.CreateEntry("test.txt");
        }

        stream.Seek(0, SeekOrigin.Begin);

        var action = async () => await NuGetNuspecUtilities.GetNuspecBytesAsync(stream);

        // No Nuspec File is in the archive
        await action.Should().ThrowAsync<FileNotFoundException>();

        // The position should always be reset to 0
        stream.Position.Should().Be(0);
    }

    [TestMethod]
    public async Task GetNuspecBytes_ReadsNuspecBytesAsync()
    {
        byte[] randomBytes = [0xDE, 0xAD, 0xC0, 0xDE];

        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("test.nuspec");

            using var entryStream = entry.Open();

            await entryStream.WriteAsync(randomBytes);
        }

        stream.Seek(0, SeekOrigin.Begin);

        var bytes = await NuGetNuspecUtilities.GetNuspecBytesAsync(stream);

        bytes.Should().HaveCount(randomBytes.Length);

        for (var i = 0; i < randomBytes.Length; i++)
        {
            bytes.Should().HaveElementAt(i, randomBytes[i]);
        }

        // The position should always be reset to 0
        stream.Position.Should().Be(0);
    }
}
