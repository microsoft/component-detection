using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Detectors.Yarn;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class YarnBlockFileTests
    {
        [TestMethod]
        public async Task BlockFileParserWithNullStream_Fails()
        {
            static async Task Action() => await YarnBlockFile.CreateBlockFileAsync(null);

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(Action);
        }

        [TestMethod]
        public async Task BlockFileParserWithClosedStream_Fails()
        {
            using var stream = new MemoryStream();

            stream.Close();

            async Task Action() => await YarnBlockFile.CreateBlockFileAsync(stream);

            await Assert.ThrowsExceptionAsync<ArgumentException>(Action);
        }

        [TestMethod]
        public async Task BlockFileParserWithEmptyStream_ProducesEnumerableOfZero()
        {
            YarnBlockFile file;
            using (var stream = new MemoryStream())
            {
                file = await YarnBlockFile.CreateBlockFileAsync(stream);
            }

            Assert.AreEqual(0, file.Count());
            Assert.AreEqual(string.Empty, file.VersionHeader);
            Assert.AreEqual(YarnLockVersion.Invalid, file.YarnLockVersion);
        }

        [TestMethod]
        public async Task BlockFileParserV1WithVersionString_ProducesEnumerableOfZero()
        {
            var yarnLockFileVersionString = "#yarn lockfile v1";

            using var stream = new MemoryStream();

            using var writer = new StreamWriter(stream);

            await writer.WriteLineAsync(yarnLockFileVersionString);
            await writer.FlushAsync();
            stream.Seek(0, SeekOrigin.Begin);

            var file = await YarnBlockFile.CreateBlockFileAsync(stream);

            Assert.AreEqual(0, file.Count());
            Assert.AreEqual(yarnLockFileVersionString, file.VersionHeader);
            Assert.AreEqual(YarnLockVersion.V1, file.YarnLockVersion);
        }

        [TestMethod]
        public async Task BlockFileParserV1WithSingleBlock_Parses()
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

            Assert.AreEqual(block.Title, "block1");
            Assert.AreEqual(1, block.Children.Count);
            Assert.AreEqual("value", block.Values["property"]);
            Assert.AreEqual("otherValue", block.Children.Single(x => x.Title == "block2").Values["otherProperty"]);
            Assert.AreEqual(yarnLockFileVersionString, file.VersionHeader);
            Assert.AreEqual(YarnLockVersion.V1, file.YarnLockVersion);
        }

        [TestMethod]
        public async Task BlockFileParserV1WithSeveralBlocks_Parses()
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

            Assert.AreEqual(3, file.Count());
            Assert.AreEqual(yarnLockFileVersionString, file.VersionHeader);
            Assert.AreEqual(YarnLockVersion.V1, file.YarnLockVersion);
        }

        [TestMethod]
        public async Task BlockFileParserV2WithMetadataBlock_Parses()
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

            Assert.AreEqual(0, file.Count());
            Assert.AreEqual(yarnLockFileVersionString, file.VersionHeader);
            Assert.AreEqual(YarnLockVersion.V2, file.YarnLockVersion);
        }

        [TestMethod]
        public async Task BlockFileParserV2WithSingleBlock_Parses()
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

            Assert.AreEqual(block.Title, "block1");
            Assert.AreEqual(1, block.Children.Count);
            Assert.AreEqual("value", block.Values["property"]);
            Assert.AreEqual("otherValue", block.Children.Single(x => x.Title == "block2").Values["otherProperty"]);
            Assert.AreEqual(yarnLockFileVersionString, file.VersionHeader);
            Assert.AreEqual(YarnLockVersion.V2, file.YarnLockVersion);
        }

        [TestMethod]
        public async Task BlockFileParserV2WithSingleBlock_ParsesWithQuotes()
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

            Assert.AreEqual(block.Title, "block1");
            Assert.AreEqual(1, block.Children.Count);
            Assert.AreEqual("value", block.Values["property"]);
            Assert.AreEqual("otherValue", block.Children.Single(x => x.Title == "block2").Values["otherProperty"]);
            Assert.AreEqual(yarnLockFileVersionString, file.VersionHeader);
            Assert.AreEqual(YarnLockVersion.V2, file.YarnLockVersion);
        }

        [TestMethod]
        public async Task BlockFileParserV2WithMultipleBlocks_Parses()
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

            Assert.AreEqual(3, file.Count());
            Assert.AreEqual(yarnLockFileVersionString, file.VersionHeader);
            Assert.AreEqual(YarnLockVersion.V2, file.YarnLockVersion);
        }
    }
}
