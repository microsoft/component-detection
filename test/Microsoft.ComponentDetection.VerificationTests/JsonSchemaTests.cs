#nullable disable
namespace Microsoft.ComponentDetection.VerificationTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;

[TestClass]
public class JsonSchemaTests
{
    private string manifestFile;
    private JSchema repoManifestSchema;
    private DirectoryInfo artifactsDir;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        var docsDir = new DirectoryInfo(Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"), "docs", "schema"));

        this.artifactsDir = new DirectoryInfo(Environment.GetEnvironmentVariable("GITHUB_NEW_ARTIFACTS_DIR"));
        this.manifestFile = await ReadFileAsync(this.artifactsDir, "ScanManifest*.json");
        this.repoManifestSchema = JSchema.Parse(await ReadFileAsync(docsDir, "manifest.schema.json"));
    }

    private static async Task<string> ReadFileAsync(DirectoryInfo dir, string pattern)
    {
        var files = dir.GetFiles(pattern);
        files.Should().HaveCountGreaterThan(0, $"There should be at least one file matching the pattern {pattern}");
        return await File.ReadAllTextAsync(files.First().FullName);
    }

    [TestMethod]
    public void CheckJsonSchemaUpdated()
    {
        var currentSchema = CreateCurrentSchema();

        // Write schema to output dir
        var schemaFile = Path.Combine(this.artifactsDir.FullName, "manifest.schema.json");
        File.WriteAllText(schemaFile, currentSchema.ToString());

        JToken.DeepEquals(this.repoManifestSchema, currentSchema).Should().BeTrue($"The schema in docs should be updated to match the current schema.");
    }

    [TestMethod]
    public void VerifyManifestConformsJsonSchema()
    {
        var manifest = JObject.Parse(this.manifestFile);
        var valid = manifest.IsValid(this.repoManifestSchema, out IList<string> errors);

        valid.Should().BeTrue($"The manifest generated from CD should conform to the JSON Schema in `docs/schema`. Errors: {string.Join(Environment.NewLine, errors)}");
    }

    private static JSchema CreateCurrentSchema()
    {
        var generator = new JSchemaGenerator();
        generator.GenerationProviders.Add(new StringEnumGenerationProvider());

        var schema = generator.Generate(typeof(ScanResult));
        schema.ExtensionData.Add("$schema", "http://json-schema.org/draft-07/schema#");

        return schema;
    }
}
