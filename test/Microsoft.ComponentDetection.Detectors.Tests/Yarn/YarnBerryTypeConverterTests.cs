﻿namespace Microsoft.ComponentDetection.Detectors.Tests.Yarn;

using FluentAssertions;
using Microsoft.ComponentDetection.Detectors.Yarn.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using YamlDotNet.Serialization;

[TestClass]
public class YarnBerryTypeConverterTests
{
    private readonly IDeserializer deserializer;

    public YarnBerryTypeConverterTests() => this.deserializer = new DeserializerBuilder().WithTypeConverter(new YarnBerryTypeConverter()).Build();

    [TestMethod]
    public void Deserialize_WithEmptyLockfile_ShouldReturnNull()
    {
        var yaml = string.Empty;

        var result = this.deserializer.Deserialize<YarnBerryLockfile>(yaml);

        result.Should().BeNull();
    }

    [TestMethod]
    public void Deserialize_WithLockfileWithMetadata_ShouldReturnLockfileWithMetadata()
    {
        var yaml = @"
__metadata:
  version: 2
  cacheKey: a";

        var result = this.deserializer.Deserialize<YarnBerryLockfile>(yaml);

        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull();
        result.Metadata.Version.Should().Be("2");
        result.Metadata.CacheKey.Should().Be("a");
    }

    [TestMethod]
    public void Deserialize_WithLockfileWithSingleEntry_ShouldReturnLockfileSingleEntry()
    {
        var yaml = @"
__metadata:
  version: 2
  cacheKey: a

""@babel/runtime-corejs3@npm:^7.10.2"":
    version: 7.17.9
    resolution: ""@babel/runtime-corejs3@npm:7.17.9""
    dependencies:
        core-js-pure: ^3.20.2
        regenerator-runtime: ^0.13.4
    checksum: c0893eb1ba4fd8a5a0e43d0fd5c3ad61c020dc5953bb74a76e9e10a0adfde7a5d8fd7e78d59b08dce3a0774948c6c40c81df0fdd0a1130c414fd3535fae365cb
    languageName: node
    linkType: hard
";

        var result = this.deserializer.Deserialize<YarnBerryLockfile>(yaml);

        result.Should().NotBeNull();
        result.Entries.Should().NotBeNull()
            .And.HaveCount(1)
            .And.ContainKey("@babel/runtime-corejs3@npm:^7.10.2");
        result.Entries.Should().ContainKey("@babel/runtime-corejs3@npm:^7.10.2");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Version.Should().Be("7.17.9");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Resolution.Should().Be("@babel/runtime-corejs3@npm:7.17.9");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Dependencies.Should().NotBeNull();
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Dependencies.Should().HaveCount(2);
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Dependencies.Should().ContainKey("core-js-pure");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Dependencies.Should().ContainKey("regenerator-runtime");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Checksum.Should().Be("c0893eb1ba4fd8a5a0e43d0fd5c3ad61c020dc5953bb74a76e9e10a0adfde7a5d8fd7e78d59b08dce3a0774948c6c40c81df0fdd0a1130c414fd3535fae365cb");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].LanguageName.Should().Be("node");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].LinkType.Should().Be("hard");
    }

    [TestMethod]
    public void Deserialize_WithLockfileWithMultipleEntries_ShouldReturnLockfileWithMultipleEntries()
    {
        var yaml = @"
__metadata:
  version: 2
  cacheKey: a

""@babel/runtime-corejs3@npm:^7.10.2"":
    version: 7.17.9
    resolution: ""@babel/runtime-corejs3@npm:7.17.9""
    dependencies:
        core-js-pure: ^3.20.2
        regenerator-runtime: ^0.13.4
    checksum: c0893eb1ba4fd8a5a0e43d0fd5c3ad61c020dc5953bb74a76e9e10a0adfde7a5d8fd7e78d59b08dce3a0774948c6c40c81df0fdd0a1130c414fd3535fae365cb
    languageName: node
    linkType: hard

""@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"":
    version: 5.6.0
    resolution: ""@ethersproject/wordlists@npm:5.6.0""
    dependencies:
        ""@ethersproject/bytes"": ^5.6.0
        ""@ethersproject/hash"": ^5.6.0
        ""@ethersproject/logger"": ^5.6.0
        ""@ethersproject/properties"": ^5.6.0
        ""@ethersproject/strings"": ^5.6.0
    checksum: 648d948d884aff09cfc11f1db404fff0489a49d50f4d878f2dbda14e02214c24e2e2efec7a3215929a5e433232413c435e41d47f2f405a46408cfd79c7f2ae78
    languageName: node
    linkType: hard
";

        var result = this.deserializer.Deserialize<YarnBerryLockfile>(yaml);

        result.Should().NotBeNull();
        result.Entries.Should().NotBeNull()
            .And.HaveCount(2)
            .And.ContainKey("@babel/runtime-corejs3@npm:^7.10.2")
            .And.ContainKey("@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0");
        result.Entries.Should().ContainKey("@babel/runtime-corejs3@npm:^7.10.2");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Version.Should().Be("7.17.9");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Resolution.Should().Be("@babel/runtime-corejs3@npm:7.17.9");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Dependencies.Should().NotBeNull();
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Dependencies.Should().HaveCount(2);
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Dependencies.Should().ContainKey("core-js-pure");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Dependencies.Should().ContainKey("regenerator-runtime");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].Checksum.Should().Be("c0893eb1ba4fd8a5a0e43d0fd5c3ad61c020dc5953bb74a76e9e10a0adfde7a5d8fd7e78d59b08dce3a0774948c6c40c81df0fdd0a1130c414fd3535fae365cb");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].LanguageName.Should().Be("node");
        result.Entries["@babel/runtime-corejs3@npm:^7.10.2"].LinkType.Should().Be("hard");
        result.Entries.Should().ContainKey("@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Version.Should().Be("5.6.0");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Resolution.Should().Be("@ethersproject/wordlists@npm:5.6.0");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Dependencies.Should().NotBeNull();
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Dependencies.Should().HaveCount(5);
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Dependencies.Should().ContainKey("@ethersproject/bytes");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Dependencies.Should().ContainKey("@ethersproject/hash");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Dependencies.Should().ContainKey("@ethersproject/logger");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Dependencies.Should().ContainKey("@ethersproject/properties");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Dependencies.Should().ContainKey("@ethersproject/strings");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].Checksum.Should().Be("648d948d884aff09cfc11f1db404fff0489a49d50f4d878f2dbda14e02214c24e2e2efec7a3215929a5e433232413c435e41d47f2f405a46408cfd79c7f2ae78");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].LanguageName.Should().Be("node");
        result.Entries["@ethersproject/wordlists@npm:5.6.0, @ethersproject/wordlists@npm:^5.6.0"].LinkType.Should().Be("hard");
    }
}
