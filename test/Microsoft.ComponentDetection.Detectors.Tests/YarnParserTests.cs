namespace Microsoft.ComponentDetection.Detectors.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Yarn;
using Microsoft.ComponentDetection.Detectors.Yarn.Parsers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class YarnParserTests
{
    private Mock<ILogger> loggerMock;

    [TestInitialize]
    public void TestInitialize() => this.loggerMock = new Mock<ILogger>();

    [TestMethod]
    public void YarnLockParserWithNullBlockFile_Fails()
    {
        var parser = new YarnLockParser();

        void Action() => parser.Parse(null, this.loggerMock.Object);

        Assert.ThrowsException<ArgumentNullException>(Action);
    }

    [TestMethod]
    public void YarnLockParser_CanParseV1LockFiles()
    {
        var yarnLockFileVersion = YarnLockVersion.V1;

        var parser = new YarnLockParser();

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);

        Assert.IsTrue(parser.CanParse(blockFile.Object.YarnLockVersion));
    }

    [TestMethod]
    public void YarnLockParser_CanParseV2LockFiles()
    {
        var yarnLockFileVersion = YarnLockVersion.V2;

        var parser = new YarnLockParser();

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);

        Assert.IsTrue(parser.CanParse(blockFile.Object.YarnLockVersion));
    }

    [TestMethod]
    public void YarnLockParser_ParsesEmptyFile()
    {
        var yarnLockFileVersion = YarnLockVersion.V1;

        var parser = new YarnLockParser();

        var blocks = Enumerable.Empty<YarnBlock>();
        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);
        blockFile.Setup(x => x.GetEnumerator()).Returns(blocks.GetEnumerator());

        var file = parser.Parse(blockFile.Object, this.loggerMock.Object);

        Assert.AreEqual(YarnLockVersion.V1, file.LockVersion);
        Assert.AreEqual(0, file.Entries.Count());
    }

    [TestMethod]
    public void YarnLockParser_ParsesBlocks()
    {
        var yarnLockFileVersion = YarnLockVersion.V1;

        var parser = new YarnLockParser();

        var blocks = new List<YarnBlock>
        {
            CreateBlock("a@^1.0.0", "1.0.0", "https://a", new List<YarnBlock>
            {
                CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2" } }),
            }),
            CreateBlock("b@2.4.6", "2.4.6", "https://b", new List<YarnBlock>
            {
                CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2.4" }, { "a", "^1.0.0" } }),
            }),
            CreateBlock("xyz@2, xyz@2.4", "2.4.3", "https://xyz", Enumerable.Empty<YarnBlock>()),
        };

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);
        blockFile.Setup(x => x.GetEnumerator()).Returns(blocks.GetEnumerator());

        var file = parser.Parse(blockFile.Object, this.loggerMock.Object);

        Assert.AreEqual(YarnLockVersion.V1, file.LockVersion);
        Assert.AreEqual(3, file.Entries.Count());

        foreach (var entry in file.Entries)
        {
            var block = blocks.Single(x => x.Values["resolved"] == entry.Resolved);

            AssertBlockMatchesEntry(block, entry);
        }
    }

    [TestMethod]
    public void YarnLockParser_ParsesNoVersionInTitleBlock()
    {
        var yarnLockFileVersion = YarnLockVersion.V1;

        var parser = new YarnLockParser();

        var blocks = new List<YarnBlock>
        {
            CreateBlock("a", "1.0.0", "https://a", new List<YarnBlock>
            {
                CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2" } }),
            }),
            CreateBlock("b", "2.4.6", "https://b", new List<YarnBlock>
            {
                CreateDependencyBlock(new Dictionary<string, string> { { "xyz", "2.4" }, { "a", "^1.0.0" } }),
            }),
        };

        var blockFile = new Mock<IYarnBlockFile>();
        blockFile.Setup(x => x.YarnLockVersion).Returns(yarnLockFileVersion);
        blockFile.Setup(x => x.GetEnumerator()).Returns(blocks.GetEnumerator());

        var file = parser.Parse(blockFile.Object, this.loggerMock.Object);

        Assert.AreEqual(YarnLockVersion.V1, file.LockVersion);
        Assert.AreEqual(2, file.Entries.Count());

        Assert.IsNotNull(file.Entries.FirstOrDefault(x => x.LookupKey == "a@1.0.0"));
        Assert.IsNotNull(file.Entries.FirstOrDefault(x => x.LookupKey == "b@2.4.6"));
    }

    private static YarnBlock CreateDependencyBlock(IDictionary<string, string> dependencies)
    {
        var block = new YarnBlock { Title = "dependencies" };

        foreach (var item in dependencies)
        {
            var version = YarnLockParser.NormalizeVersion(item.Value);
            block.Values[item.Key] = version;
        }

        return block;
    }

    private static YarnBlock CreateBlock(string title, string version, string resolved, IEnumerable<YarnBlock> dependencies)
    {
        var block = new YarnBlock
        {
            Title = title,
            Values =
            {
                ["version"] = version,
                ["resolved"] = resolved,
            },
        };

        foreach (var dependency in dependencies)
        {
            block.Children.Add(dependency);
        }

        return block;
    }

    private static void AssertBlockMatchesEntry(YarnBlock block, YarnEntry entry)
    {
        var componentName = block.Title.Split(',').Select(x => x.Trim()).First().Split('@')[0];
        var blockVersions = block.Title.Split(',').Select(x => x.Trim()).Select(x => x.Split('@')[1]);

        Assert.AreEqual(componentName, entry.Name);

        foreach (var version in blockVersions)
        {
            Assert.IsTrue(entry.Satisfied.Contains(YarnLockParser.NormalizeVersion(version)));
        }

        Assert.AreEqual(block.Values["version"], entry.Version);
        Assert.AreEqual(block.Values["resolved"], entry.Resolved);

        var dependencies = block.Children.SingleOrDefault(x => x.Title == "dependencies");

        if (dependencies != null)
        {
            foreach (var dependency in dependencies.Values)
            {
                Assert.IsNotNull(entry.Dependencies.SingleOrDefault(x => x.Name == dependency.Key && x.Version == dependency.Value));
            }
        }
    }
}
