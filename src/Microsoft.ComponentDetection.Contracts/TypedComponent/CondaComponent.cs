#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class CondaComponent : TypedComponent
{
    public CondaComponent(string name, string version, string build, string channel, string subdir, string @namespace, string url, string md5, string sha256 = null)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Conda));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Conda));
        this.Build = build;
        this.Channel = channel;
        this.Subdir = subdir;
        this.Namespace = @namespace;
        this.Url = url;
        this.MD5 = md5;
        this.SHA256 = sha256;

        if (Uri.TryCreate(url, UriKind.Absolute, out var downloadUrl))
        {
            this.DownloadUrl = downloadUrl;
        }
    }

    public CondaComponent()
    {
        /* Reserved for deserialization */
    }

    [JsonPropertyName("build")]
    public string Build { get; set; }

    [JsonPropertyName("channel")]
    public string Channel { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; }

    [JsonPropertyName("subdir")]
    public string Subdir { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("mD5")]
    public string MD5 { get; set; }

    [JsonPropertyName("sha256")]
    public string SHA256 { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Conda;

    protected override string ComputeBaseId() => $"{this.Name} {this.Version} - {this.Type}";

    /// <summary>
    /// Uses native Conda coordinates to distinguish artifacts while excluding mirror URLs and
    /// integrity hashes from component identity.
    /// </summary>
    /// <returns>The Conda coordinates included in the extended component identity.</returns>
    protected override IEnumerable<KeyValuePair<string, string>> GetExtendedIdProperties()
    {
        if (!string.IsNullOrWhiteSpace(this.Build))
        {
            yield return new KeyValuePair<string, string>(nameof(this.Build), this.Build);
        }

        if (!string.IsNullOrWhiteSpace(this.Channel))
        {
            yield return new KeyValuePair<string, string>(nameof(this.Channel), this.Channel);
        }

        if (!string.IsNullOrWhiteSpace(this.Subdir))
        {
            yield return new KeyValuePair<string, string>(nameof(this.Subdir), this.Subdir);
        }
    }
}
