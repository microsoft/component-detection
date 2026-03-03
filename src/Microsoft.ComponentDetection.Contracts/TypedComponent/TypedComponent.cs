#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using PackageUrl;
using JsonConverterAttribute = Newtonsoft.Json.JsonConverterAttribute;
using JsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;
using SystemTextJson = System.Text.Json.Serialization;

[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
[JsonConverter(typeof(TypedComponentConverter))] // Newtonsoft.Json
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[SystemTextJson.JsonConverter(typeof(TypedComponentSystemTextJsonConverter))] // System.Text.Json
public abstract class TypedComponent
{
#pragma warning disable IDE0032 // Use auto property - backing fields needed for lazy ??= initialization
    [JsonIgnore] // Newtonsoft.Json
    [SystemTextJson.JsonIgnore] // System.Text.Json
    private string id;

    [JsonIgnore] // Newtonsoft.Json
    [SystemTextJson.JsonIgnore] // System.Text.Json
    private string baseId;
#pragma warning restore IDE0032

    internal TypedComponent()
    {
        // Reserved for deserialization.
    }

    /// <summary>Gets the type of the component, must be well known.</summary>
    [JsonConverter(typeof(StringEnumConverter))] // Newtonsoft.Json
    [JsonProperty("type", Order = int.MinValue)] // Newtonsoft.Json
    [SystemTextJson.JsonIgnore] // System.Text.Json - type is handled by TypedComponentSystemTextJsonConverter
    public abstract ComponentType Type { get; }

    /// <summary>
    /// Gets the unique identifier for this component, incorporating both required identity fields
    /// (e.g., name, version, type) and optional provenance metadata (download URL, source URL) when available.
    /// When no optional metadata is present, this is identical to <see cref="BaseId"/>.
    /// When optional metadata is present, the format is: <c>BaseId [optionalProp1:value1 optionalProp2:value2]</c>.
    /// </summary>
    [JsonProperty("id")] // Newtonsoft.Json
    [SystemTextJson.JsonPropertyName("id")] // System.Text.Json
    public string Id => this.id ??= this.ComputeId();

    /// <summary>
    /// Gets the base identifier for this component, derived solely from required identity fields
    /// (e.g., name, version, type). Use this when comparing components by package identity alone,
    /// without considering provenance metadata such as download or source URLs.
    /// </summary>
    [JsonIgnore] // Newtonsoft.Json
    [SystemTextJson.JsonIgnore] // System.Text.Json
    public string BaseId => this.baseId ??= this.ComputeBaseId();

    [SystemTextJson.JsonPropertyName("packageUrl")]
    public virtual PackageURL PackageUrl { get; }

    /// <summary>Gets or sets SPDX license expression(s) declared by the package author.</summary>
    [SystemTextJson.JsonIgnore(Condition = SystemTextJson.JsonIgnoreCondition.WhenWritingNull)]
    [SystemTextJson.JsonPropertyName("licenses")]
    public virtual IList<string> Licenses { get; set; }

    /// <summary>Gets or sets structured author/creator identity (SPDX 3.0.1 originatedBy).</summary>
    [SystemTextJson.JsonIgnore(Condition = SystemTextJson.JsonIgnoreCondition.WhenWritingNull)]
    [SystemTextJson.JsonPropertyName("authorsInfo")]
    public virtual IList<ActorInfo> AuthorsInfo { get; set; }

    /// <summary>Gets or sets the direct download URL for the package binary.</summary>
    [SystemTextJson.JsonIgnore(Condition = SystemTextJson.JsonIgnoreCondition.WhenWritingNull)]
    [SystemTextJson.JsonPropertyName("downloadUrl")]
    public virtual Uri DownloadUrl { get; set; }

    /// <summary>Gets or sets the source code repository URL.</summary>
    [SystemTextJson.JsonIgnore(Condition = SystemTextJson.JsonIgnoreCondition.WhenWritingNull)]
    [SystemTextJson.JsonPropertyName("sourceUrl")]
    public virtual Uri SourceUrl { get; set; }

    [JsonIgnore] // Newtonsoft.Json
    [SystemTextJson.JsonIgnore] // System.Text.Json
    internal string DebuggerDisplay => $"{this.Id}";

    protected string ValidateRequiredInput(string input, string fieldName, string componentType)
    {
        return string.IsNullOrWhiteSpace(input)
            ? throw new ArgumentNullException(fieldName, this.NullPropertyExceptionMessage(fieldName, componentType))
            : input;
    }

    protected T ValidateRequiredInput<T>(T input, string fieldName, string componentType)
    {
        // Null coalescing for generic types is not available until C# 8
        return EqualityComparer<T>.Default.Equals(input, default(T)) ? throw new ArgumentNullException(fieldName, this.NullPropertyExceptionMessage(fieldName, componentType)) : input;
    }

    protected string NullPropertyExceptionMessage(string propertyName, string componentType)
    {
        return $"Property {propertyName} of component type {componentType} is required";
    }

    /// <summary>Computes the base identity string from required fields. Subclasses must implement this.</summary>
    /// <returns>The base identity string for this component.</returns>
    protected abstract string ComputeBaseId();

    private string ComputeId()
    {
        var baseId = this.ComputeBaseId();
        if (this.DownloadUrl == null && this.SourceUrl == null)
        {
            return baseId;
        }

        var extended = baseId + " [";
        var hasDownload = this.DownloadUrl != null;
        if (hasDownload)
        {
            extended += $"{nameof(this.DownloadUrl)}:{this.DownloadUrl}";
        }

        if (this.SourceUrl != null)
        {
            if (hasDownload)
            {
                extended += " ";
            }

            extended += $"{nameof(this.SourceUrl)}:{this.SourceUrl}";
        }

        return extended + "]";
    }
}
