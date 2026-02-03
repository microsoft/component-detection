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
    [JsonIgnore] // Newtonsoft.Json
    [SystemTextJson.JsonIgnore] // System.Text.Json
    private string id;

    internal TypedComponent()
    {
        // Reserved for deserialization.
    }

    /// <summary>Gets the type of the component, must be well known.</summary>
    [JsonConverter(typeof(StringEnumConverter))] // Newtonsoft.Json
    [JsonProperty("type", Order = int.MinValue)] // Newtonsoft.Json
    [SystemTextJson.JsonIgnore] // System.Text.Json - type is handled by TypedComponentSystemTextJsonConverter
    public abstract ComponentType Type { get; }

    /// <summary>Gets the id of the component.</summary>
    [JsonProperty("id")] // Newtonsoft.Json
    [SystemTextJson.JsonPropertyName("id")] // System.Text.Json
    public string Id => this.id ??= this.ComputeId();

    [SystemTextJson.JsonPropertyName("packageUrl")]
    public virtual PackageURL PackageUrl { get; }

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

    protected abstract string ComputeId();
}
