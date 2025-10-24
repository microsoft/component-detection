#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.Yarn;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class YarnBlockFileTests
{
    [TestMethod]
    public async Task BlockFileParserWithNullStream_FailsAsync()
    {
        var action = async () => await YarnBlockFile.CreateBlockFileAsync(null);
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public async Task BlockFileParserWithClosedStream_FailsAsync()
    {
        using var stream = new MemoryStream();

        stream.Close();

        var action = async () => await YarnBlockFile.CreateBlockFileAsync(stream);
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public async Task BlockFileParserWithEmptyStream_ProducesEnumerableOfZeroAsync()
    {
        YarnBlockFile file;
        using (var stream = new MemoryStream())
        {
            file = await YarnBlockFile.CreateBlockFileAsync(stream);
        }

        file.Should().BeEmpty();
        file.VersionHeader.Should().Be(string.Empty);
        file.YarnLockVersion.Should().Be(YarnLockVersion.Invalid);
    }

    [TestMethod]
    public async Task BlockFileParserV1WithVersionString_ProducesEnumerableOfZeroAsync()
    {
        var yarnLockFileVersionString = "#yarn lockfile v1";

        using var stream = new MemoryStream();

        using var writer = new StreamWriter(stream);

        await writer.WriteLineAsync(yarnLockFileVersionString);
        await writer.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        var file = await YarnBlockFile.CreateBlockFileAsync(stream);

        file.Should().BeEmpty();
        file.VersionHeader.Should().Be(yarnLockFileVersionString);
        file.YarnLockVersion.Should().Be(YarnLockVersion.V1);
    }

    [TestMethod]
    public async Task BlockFileParserV1WithSingleBlock_ParsesAsync()
    {
        var yarnLockFileVersionString = "#yarn lockfile v1";

        using var stream = new MemoryStream();

        using var writer = new StreamWriter(stream);

        await writer.WriteLineAsync(yarnLockFileVersionString);
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("block1:");
        await writer.WriteLineAsync("  property \"value\"");
        await writer.WriteLineAsync("  block2:");
        await writer.WriteLineAsync("    otherProperty \"otherValue\"");

        await writer.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        var file = await YarnBlockFile.CreateBlockFileAsync(stream);

        var block = file.Single();

        block.Title.Should().Be("block1");
        block.Children.Should().ContainSingle();
        block.Values["property"].Should().Be("value");
        block.Children.Single(x => x.Title == "block2").Values.Should().ContainKey("otherProperty");
        var value = block.Children.Single(x => x.Title == "block2").Values["otherProperty"];
        value.Should().Be("otherValue");
        file.VersionHeader.Should().Be(yarnLockFileVersionString);
        file.YarnLockVersion.Should().Be(YarnLockVersion.V1);
    }

    [TestMethod]
    public async Task BlockFileParserV1WithSeveralBlocks_ParsesAsync()
    {
        var yarnLockFileVersionString = "#yarn lockfile v1";

        using var stream = new MemoryStream();

        using var writer = new StreamWriter(stream);

        await writer.WriteLineAsync(yarnLockFileVersionString);
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("block1:");
        await writer.WriteLineAsync("  property \"value\"");
        await writer.WriteLineAsync("  childblock1:");
        await writer.WriteLineAsync("    otherProperty \"otherValue\"");

        await writer.WriteLineAsync();

        await writer.WriteLineAsync(yarnLockFileVersionString);
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("block2:");
        await writer.WriteLineAsync("  property \"value\"");
        await writer.WriteLineAsync("  childBlock2:");
        await writer.WriteLineAsync("    otherProperty \"otherValue\"");

        await writer.WriteLineAsync();

        await writer.WriteLineAsync(yarnLockFileVersionString);
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("block3:");
        await writer.WriteLineAsync("  property \"value\"");
        await writer.WriteLineAsync("  childBlock3:");
        await writer.WriteLineAsync("    otherProperty \"otherValue\"");

        await writer.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        var file = await YarnBlockFile.CreateBlockFileAsync(stream);

        file.Should().HaveCount(3);
        file.VersionHeader.Should().Be(yarnLockFileVersionString);
        file.YarnLockVersion.Should().Be(YarnLockVersion.V1);
    }

    [TestMethod]
    public async Task BlockFileParserV2WithMetadataBlock_ParsesAsync()
    {
        var yarnLockFileVersionString = "__metadata:";

        using var stream = new MemoryStream();

        using var writer = new StreamWriter(stream);

        await writer.WriteLineAsync("# This file is generated by running \"yarn install\" inside your project.");
        await writer.WriteLineAsync("# Manual changes might be lost - proceed with caution!");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("__metadata:");
        await writer.WriteLineAsync("  version: 4");
        await writer.WriteLineAsync("  cacheKey: 7");
        await writer.WriteLineAsync();

        await writer.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        var file = await YarnBlockFile.CreateBlockFileAsync(stream);

        file.Should().BeEmpty();
        file.VersionHeader.Should().Be(yarnLockFileVersionString);
        file.YarnLockVersion.Should().Be(YarnLockVersion.Berry);
    }

    [TestMethod]
    public async Task BlockFileParserV2WithSingleBlock_ParsesAsync()
    {
        var yarnLockFileVersionString = "__metadata:";

        using var stream = new MemoryStream();

        using var writer = new StreamWriter(stream);

        await writer.WriteLineAsync("# This file is generated by running \"yarn install\" inside your project.");
        await writer.WriteLineAsync("# Manual changes might be lost - proceed with caution!");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("__metadata:");
        await writer.WriteLineAsync("  version: 4");
        await writer.WriteLineAsync("  cacheKey: 7");

        await writer.WriteLineAsync();

        await writer.WriteLineAsync("block1:");
        await writer.WriteLineAsync("  property: value");
        await writer.WriteLineAsync("  block2:");
        await writer.WriteLineAsync("    otherProperty: otherValue");

        await writer.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        var file = await YarnBlockFile.CreateBlockFileAsync(stream);

        var block = file.Single();

        block.Title.Should().Be("block1");
        block.Children.Should().ContainSingle();
        block.Values["property"].Should().Be("value");
        block.Children.Single(x => x.Title == "block2").Values.Should().ContainKey("otherProperty");
        var value = block.Children.Single(x => x.Title == "block2").Values["otherProperty"];
        file.VersionHeader.Should().Be(yarnLockFileVersionString);
        file.YarnLockVersion.Should().Be(YarnLockVersion.Berry);
    }

    [TestMethod]
    public async Task BlockFileParserV2WithSingleBlock_ParsesWithQuotesAsync()
    {
        var yarnLockFileVersionString = "__metadata:";

        using var stream = new MemoryStream();

        using var writer = new StreamWriter(stream);

        await writer.WriteLineAsync("# This file is generated by running \"yarn install\" inside your project.");
        await writer.WriteLineAsync("# Manual changes might be lost - proceed with caution!");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("__metadata:");
        await writer.WriteLineAsync("  version: 4");
        await writer.WriteLineAsync("  cacheKey: 7");

        await writer.WriteLineAsync();

        await writer.WriteLineAsync("block1:");
        await writer.WriteLineAsync("  property: \"value\"");
        await writer.WriteLineAsync("  block2:");
        await writer.WriteLineAsync("    \"otherProperty\": otherValue");

        await writer.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        var file = await YarnBlockFile.CreateBlockFileAsync(stream);

        var block = file.Single();

        block.Title.Should().Be("block1");
        block.Children.Should().ContainSingle();
        block.Values["property"].Should().Be("value");
        block.Children.Single(x => x.Title == "block2").Values.Should().ContainKey("otherProperty");
        var value = block.Children.Single(x => x.Title == "block2").Values["otherProperty"];
        file.VersionHeader.Should().Be(yarnLockFileVersionString);
        file.YarnLockVersion.Should().Be(YarnLockVersion.Berry);
    }

    [TestMethod]
    public async Task BlockFileParserV2WithMultipleBlocks_ParsesAsync()
    {
        var yarnLockFileVersionString = "__metadata:";

        using var stream = new MemoryStream();

        using var writer = new StreamWriter(stream);

        await writer.WriteLineAsync("# This file is generated by running \"yarn install\" inside your project.");
        await writer.WriteLineAsync("# Manual changes might be lost - proceed with caution!");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("__metadata:");
        await writer.WriteLineAsync("  version: 4");
        await writer.WriteLineAsync("  cacheKey: 7");

        await writer.WriteLineAsync();

        await writer.WriteLineAsync("block1:");
        await writer.WriteLineAsync("  property: value");
        await writer.WriteLineAsync("  childblock1:");
        await writer.WriteLineAsync("    otherProperty: otherValue");

        await writer.WriteLineAsync();

        await writer.WriteLineAsync("block2:");
        await writer.WriteLineAsync("  property: value");
        await writer.WriteLineAsync("  childblock2:");
        await writer.WriteLineAsync("    otherProperty: otherValue");

        await writer.WriteLineAsync();

        await writer.WriteLineAsync("block3:");
        await writer.WriteLineAsync("  property: value");
        await writer.WriteLineAsync("  childblock3:");
        await writer.WriteLineAsync("    otherProperty: otherValue");

        await writer.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        var file = await YarnBlockFile.CreateBlockFileAsync(stream);

        file.Should().HaveCount(3);
        file.VersionHeader.Should().Be(yarnLockFileVersionString);
        file.YarnLockVersion.Should().Be(YarnLockVersion.Berry);
    }
}
