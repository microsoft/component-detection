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
            Func<Task> action = async () => await YarnBlockFile.CreateBlockFileAsync(null);

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(action);
        }

        [TestMethod]
        public async Task BlockFileParserWithClosedStream_Fails()
        {
            using var stream = new MemoryStream();

            stream.Close();

            Func<Task> action = async () => await YarnBlockFile.CreateBlockFileAsync(stream);

            await Assert.ThrowsExceptionAsync<ArgumentException>(action);
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

            writer.WriteLine(yarnLockFileVersionString);
            writer.Flush();
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

            writer.WriteLine(yarnLockFileVersionString);
            writer.WriteLine();
            writer.WriteLine("block1:");
            writer.WriteLine("  property \"value\"");
            writer.WriteLine("  block2:");
            writer.WriteLine("    otherProperty \"otherValue\"");

            writer.Flush();
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

            writer.WriteLine(yarnLockFileVersionString);
            writer.WriteLine();
            writer.WriteLine("block1:");
            writer.WriteLine("  property \"value\"");
            writer.WriteLine("  childblock1:");
            writer.WriteLine("    otherProperty \"otherValue\"");

            writer.WriteLine();

            writer.WriteLine(yarnLockFileVersionString);
            writer.WriteLine();
            writer.WriteLine("block2:");
            writer.WriteLine("  property \"value\"");
            writer.WriteLine("  childBlock2:");
            writer.WriteLine("    otherProperty \"otherValue\"");

            writer.WriteLine();

            writer.WriteLine(yarnLockFileVersionString);
            writer.WriteLine();
            writer.WriteLine("block3:");
            writer.WriteLine("  property \"value\"");
            writer.WriteLine("  childBlock3:");
            writer.WriteLine("    otherProperty \"otherValue\"");

            writer.Flush();
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

            writer.WriteLine("# This file is generated by running \"yarn install\" inside your project.");
            writer.WriteLine("# Manual changes might be lost - proceed with caution!");
            writer.WriteLine();
            writer.WriteLine("__metadata:");
            writer.WriteLine("  version: 4");
            writer.WriteLine("  cacheKey: 7");
            writer.WriteLine();

            writer.Flush();
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

            writer.WriteLine("# This file is generated by running \"yarn install\" inside your project.");
            writer.WriteLine("# Manual changes might be lost - proceed with caution!");
            writer.WriteLine();
            writer.WriteLine("__metadata:");
            writer.WriteLine("  version: 4");
            writer.WriteLine("  cacheKey: 7");

            writer.WriteLine();

            writer.WriteLine("block1:");
            writer.WriteLine("  property: value");
            writer.WriteLine("  block2:");
            writer.WriteLine("    otherProperty: otherValue");

            writer.Flush();
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

            writer.WriteLine("# This file is generated by running \"yarn install\" inside your project.");
            writer.WriteLine("# Manual changes might be lost - proceed with caution!");
            writer.WriteLine();
            writer.WriteLine("__metadata:");
            writer.WriteLine("  version: 4");
            writer.WriteLine("  cacheKey: 7");

            writer.WriteLine();

            writer.WriteLine("block1:");
            writer.WriteLine("  property: \"value\"");
            writer.WriteLine("  block2:");
            writer.WriteLine("    \"otherProperty\": otherValue");

            writer.Flush();
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

            writer.WriteLine("# This file is generated by running \"yarn install\" inside your project.");
            writer.WriteLine("# Manual changes might be lost - proceed with caution!");
            writer.WriteLine();
            writer.WriteLine("__metadata:");
            writer.WriteLine("  version: 4");
            writer.WriteLine("  cacheKey: 7");

            writer.WriteLine();

            writer.WriteLine("block1:");
            writer.WriteLine("  property: value");
            writer.WriteLine("  childblock1:");
            writer.WriteLine("    otherProperty: otherValue");

            writer.WriteLine();

            writer.WriteLine("block2:");
            writer.WriteLine("  property: value");
            writer.WriteLine("  childblock2:");
            writer.WriteLine("    otherProperty: otherValue");

            writer.WriteLine();

            writer.WriteLine("block3:");
            writer.WriteLine("  property: value");
            writer.WriteLine("  childblock3:");
            writer.WriteLine("    otherProperty: otherValue");

            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            var file = await YarnBlockFile.CreateBlockFileAsync(stream);

            Assert.AreEqual(3, file.Count());
            Assert.AreEqual(yarnLockFileVersionString, file.VersionHeader);
            Assert.AreEqual(YarnLockVersion.V2, file.YarnLockVersion);
        }
    }
}
