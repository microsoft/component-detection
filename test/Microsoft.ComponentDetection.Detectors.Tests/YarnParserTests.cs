#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Yarn;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class YarnParserTests
{
    private readonly Mock<ILogger<YarnLockParser>> loggerMock;
    private readonly Mock<ISingleFileComponentRecorder> recorderMock;

    public YarnParserTests()
    {
        this.loggerMock = new Mock<ILogger<YarnLockParser>>();
        this.recorderMock = new Mock<ISingleFileComponentRecorder>();
    }

    [TestMethod]
    public void YarnLockParserWithNullBlockFile_Fails()
    {
        var parser = new YarnLockParser(this.loggerMock.Object);

        var action = () => parser.Parse(this.recorderMock.Object, null, this.loggerMock.Object);

        action.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void YarnLockParser_CanParseV1LockFiles()
    {
        var yarnLockFileVersion = YarnLockVersion.V1;

        var parser = new YarnLockParser(this.loggerMock.Object);

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);

        parser.CanParse(blockFile.Object.YarnLockVersion).Should().BeTrue();
    }

    [TestMethod]
    public void YarnLockParser_CanParseV2LockFiles()
    {
        var yarnLockFileVersion = YarnLockVersion.Berry;

        var parser = new YarnLockParser(this.loggerMock.Object);

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);

        parser.CanParse(blockFile.Object.YarnLockVersion).Should().BeTrue();
    }

    [TestMethod]
    public void YarnLockParser_ParsesEmptyFile()
    {
        var yarnLockFileVersion = YarnLockVersion.V1;

        var parser = new YarnLockParser(this.loggerMock.Object);

        var blocks = Enumerable.Empty<YarnBlock>();
        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);
        blockFile.Setup(x => x.GetEnumerator()).Returns(blocks.GetEnumerator());

        var file = parser.Parse(this.recorderMock.Object, blockFile.Object, this.loggerMock.Object);

        file.LockVersion.Should().Be(YarnLockVersion.V1);
        file.Entries.Should().BeEmpty();
    }

    [TestMethod]
    public void YarnLockParser_V1_ParsesBlocks()
    {
        var yarnLockFileVersion = YarnLockVersion.V1;

        var parser = new YarnLockParser(this.loggerMock.Object);

        var blocks = new List<YarnBlock>
        {
            this.CreateBlock(
                "a@^1.0.0",
                "1.0.0",
                "https://a",
                [
                    this.CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2" } }),
                ],
                yarnLockFileVersion),
            this.CreateBlock(
                "b@2.4.6",
                "2.4.6",
                "https://b",
                [
                    this.CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2.4" }, { "a", "^1.0.0" } }),
                ],
                yarnLockFileVersion),
            this.CreateBlock("xyz@2, xyz@2.4", "2.4.3", "https://xyz", [], yarnLockFileVersion),
        };

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);
        blockFile.Setup(x => x.GetEnumerator()).Returns(blocks.GetEnumerator());

        var file = parser.Parse(this.recorderMock.Object, blockFile.Object, this.loggerMock.Object);

        file.LockVersion.Should().Be(yarnLockFileVersion);
        file.Entries.Should().HaveCount(3);

        foreach (var entry in file.Entries)
        {
            var block = blocks.Single(x => x.Values[this.GetResolvedEntryName(yarnLockFileVersion)] == entry.Resolved);

            this.AssertBlockMatchesEntry(block, entry, yarnLockFileVersion);
        }
    }

    [TestMethod]
    public void YarnLockParser_Berry_ParsesBlocks()
    {
        var yarnLockFileVersion = YarnLockVersion.Berry;

        var parser = new YarnLockParser(this.loggerMock.Object);

        var blocks = new List<YarnBlock>
        {
            this.CreateBlock(
                "a@^1.0.0",
                "1.0.0",
                "https://a",
                [
                    this.CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2" } }),
                ],
                yarnLockFileVersion),
            this.CreateBlock(
                "b@2.4.6",
                "2.4.6",
                "https://b",
                [
                    this.CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2.4" }, { "a", "^1.0.0" } }),
                ],
                yarnLockFileVersion),
            this.CreateBlock("xyz@2, xyz@2.4", "2.4.3", "https://xyz", [], yarnLockFileVersion),
        };

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);
        blockFile.Setup(x => x.GetEnumerator()).Returns(blocks.GetEnumerator());

        var file = parser.Parse(this.recorderMock.Object, blockFile.Object, this.loggerMock.Object);

        file.LockVersion.Should().Be(yarnLockFileVersion);
        file.Entries.Should().HaveCount(3);

        foreach (var entry in file.Entries)
        {
            var block = blocks.Single(x => x.Values[this.GetResolvedEntryName(yarnLockFileVersion)] == entry.Resolved);

            this.AssertBlockMatchesEntry(block, entry, yarnLockFileVersion);
        }
    }

    [TestMethod]
    public void YarnLockParser_Berry_SkipsWorkspaceEntries()
    {
        var yarnLockFileVersion = YarnLockVersion.Berry;

        var parser = new YarnLockParser(this.loggerMock.Object);

        var blocks = new List<YarnBlock>
        {
            this.CreateBlock(
                "internal-package@npm:0.0.0, internal-package@workspace:packages/internal-package",
                "0.0.0-use.local",
                "internal-package@workspace:packages/internal-package",
                [
                    this.CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2" } }),
                ],
                yarnLockFileVersion),
            this.CreateBlock("xyz@2, xyz@2.4", "2.4.3", "https://xyz", [], yarnLockFileVersion),
        };

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);
        blockFile.Setup(x => x.GetEnumerator()).Returns(blocks.GetEnumerator());

        var file = parser.Parse(this.recorderMock.Object, blockFile.Object, this.loggerMock.Object);

        file.LockVersion.Should().Be(yarnLockFileVersion);
        file.Entries.Should().ContainSingle();

        foreach (var entry in file.Entries)
        {
            var block = blocks.Single(x => x.Values[this.GetResolvedEntryName(yarnLockFileVersion)] == entry.Resolved);

            this.AssertBlockMatchesEntry(block, entry, yarnLockFileVersion);
        }
    }

    [TestMethod]
    public void YarnLockParser_ParsesNoVersionInTitleBlock()
    {
        var yarnLockFileVersion = YarnLockVersion.V1;

        var parser = new YarnLockParser(this.loggerMock.Object);

        var blocks = new List<YarnBlock>
        {
            this.CreateBlock("a", "1.0.0", "https://a", [
                this.CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2" } }),
            ]),
            this.CreateBlock("b", "2.4.6", "https://b", [
                this.CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2.4" }, { "a", "^1.0.0" } }),
            ]),
        };

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);
        blockFile.Setup(x => x.GetEnumerator()).Returns(blocks.GetEnumerator());

        var file = parser.Parse(this.recorderMock.Object, blockFile.Object, this.loggerMock.Object);

        file.LockVersion.Should().Be(YarnLockVersion.V1);
        file.Entries.Should().HaveCount(2);

        file.Entries.FirstOrDefault(x => x.LookupKey == "a@1.0.0").Should().NotBeNull();
        file.Entries.FirstOrDefault(x => x.LookupKey == "b@2.4.6").Should().NotBeNull();
    }

    private YarnBlock CreateDependencyBlock(IDictionary<string, string> dependencies)
    {
        var block = new YarnBlock { Title = "dependencies" };

        foreach (var item in dependencies)
        {
            var version = YarnLockParser.NormalizeVersion(item.Value);
            block.Values[item.Key] = version;
        }

        return block;
    }

    private YarnBlock CreateBlock(string title, string version, string resolved, IEnumerable<YarnBlock> dependencies, YarnLockVersion lockfileVersion = YarnLockVersion.V1)
    {
        var block = new YarnBlock
        {
            Title = title,
            Values =
            {
                ["version"] = version,
                [this.GetResolvedEntryName(lockfileVersion)] = resolved,
            },
        };

        foreach (var dependency in dependencies)
        {
            block.Children.Add(dependency);
        }

        return block;
    }

    private void AssertBlockMatchesEntry(YarnBlock block, YarnEntry entry, YarnLockVersion lockfileVersion = YarnLockVersion.V1)
    {
        var componentName = block.Title.Split(',').Select(x => x.Trim()).First().Split('@')[0];
        var blockVersions = block.Title.Split(',').Select(x => x.Trim()).Select(x => x.Split('@')[1]);

        entry.Name.Should().Be(componentName);

        foreach (var version in blockVersions)
        {
            entry.Satisfied.Should().Contain(YarnLockParser.NormalizeVersion(version));
        }

        entry.Version.Should().Be(block.Values["version"]);
        entry.Resolved.Should().Be(block.Values[this.GetResolvedEntryName(lockfileVersion)]);

        var dependencies = block.Children.SingleOrDefault(x => x.Title == "dependencies");

        if (dependencies != null)
        {
            foreach (var dependency in dependencies.Values)
            {
                entry.Dependencies.SingleOrDefault(x => x.Name == dependency.Key && x.Version == dependency.Value).Should().NotBeNull();
            }
        }
    }

    private string GetResolvedEntryName(YarnLockVersion lockfileVersion)
    {
        return lockfileVersion == YarnLockVersion.Berry ? "resolution" : "resolved";
    }
}
