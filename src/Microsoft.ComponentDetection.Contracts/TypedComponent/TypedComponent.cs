#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using PackageUrl;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonConverter(typeof(TypedComponentSystemTextJsonConverter))]
public abstract class TypedComponent
{
#pragma warning disable IDE0032 // Use auto property - backing fields needed for lazy ??= initialization
    [JsonIgnore]
    private string id;

    [JsonIgnore]
    private string baseId;
#pragma warning restore IDE0032

    internal TypedComponent()
    {
        // Reserved for deserialization.
    }

    /// <summary>Gets the type of the component, must be well known.</summary>
    [JsonIgnore] // type is handled by TypedComponentSystemTextJsonConverter
    public abstract ComponentType Type { get; }

    /// <summary>
    /// Gets the unique identifier for this component, incorporating both required identity fields
    /// (e.g., name, version, type) and optional provenance metadata (download URL, source URL) when available.
    /// When no optional metadata is present, this is identical to <see cref="BaseId"/>.
    /// When optional metadata is present, the format is: <c>BaseId [optionalProp1:value1 optionalProp2:value2]</c>.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id => this.id ??= this.ComputeId();

    /// <summary>
    /// Gets the base identifier for this component, derived solely from required identity fields
    /// (e.g., name, version, type). Use this when comparing components by package identity alone,
    /// without considering provenance metadata such as download or source URLs.
    /// </summary>
    [JsonIgnore]
    public string BaseId => this.baseId ??= this.ComputeBaseId();

    [JsonPropertyName("packageUrl")]
    public virtual PackageURL PackageUrl { get; }

    /// <summary>Gets or sets SPDX license expression(s) declared by the package author.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("licenses")]
    public virtual IList<string> Licenses { get; set; }

    /// <summary>Gets or sets structured author/creator identity (SPDX 3.0.1 originatedBy).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("authorsInfo")]
    public virtual IList<ActorInfo> AuthorsInfo { get; set; }

    /// <summary>Gets or sets the direct download URL for the package binary.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("downloadUrl")]
    public virtual Uri DownloadUrl { get; set; }

    /// <summary>Gets or sets the source code repository URL.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("sourceUrl")]
    public virtual Uri SourceUrl { get; set; }

    [JsonIgnore]
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

    /// <summary>
    /// Returns optional properties to include in the extended component identity.
    /// Subclasses may override to exclude properties already present in <see cref="ComputeBaseId"/>.
    /// </summary>
    /// <returns>Key-value pairs to append to the base identity.</returns>
    protected virtual IEnumerable<KeyValuePair<string, string>> GetExtendedIdProperties()
    {
        if (this.DownloadUrl != null)
        {
            yield return new KeyValuePair<string, string>(nameof(this.DownloadUrl), this.DownloadUrl.ToString());
        }

        if (this.SourceUrl != null)
        {
            yield return new KeyValuePair<string, string>(nameof(this.SourceUrl), this.SourceUrl.ToString());
        }
    }

    private string ComputeId()
    {
        var baseId = this.baseId ?? this.ComputeBaseId();
        var extras = this.GetExtendedIdProperties().ToList();

        if (extras.Count == 0)
        {
            return baseId;
        }

        return baseId + " [" + string.Join(" ", extras.Select(e => $"{e.Key}:{e.Value}")) + "]";
    }
}