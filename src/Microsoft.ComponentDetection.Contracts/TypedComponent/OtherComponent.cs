#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class OtherComponent : TypedComponent
{
    public OtherComponent()
    {
        /* Reserved for deserialization */
    }

    public OtherComponent(string name, string version, Uri downloadUrl, string hash)
    {
        this.Name = this.ValidateRequiredInput(name, nameof(this.Name), nameof(ComponentType.Other));
        this.Version = this.ValidateRequiredInput(version, nameof(this.Version), nameof(ComponentType.Other));
        this.DownloadUrl = this.ValidateRequiredInput(downloadUrl, nameof(this.DownloadUrl), nameof(ComponentType.Other));
        this.Hash = hash;
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonIgnore]
    public override ComponentType Type => ComponentType.Other;

    protected override string ComputeBaseId() => $"{this.Name} {this.Version} {this.DownloadUrl} - {this.Type}";

    /// <summary>DownloadUrl is already part of the base identity for OtherComponent, so exclude it here.</summary>
    /// <returns>Extended identity properties excluding DownloadUrl.</returns>
    protected override IEnumerable<KeyValuePair<string, string>> GetExtendedIdProperties()
    {
        if (this.SourceUrl != null)
        {
            yield return new KeyValuePair<string, string>(nameof(this.SourceUrl), this.SourceUrl.ToString());
        }
    }
}
