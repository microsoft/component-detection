#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using PackageUrl;
using JsonConverterAttribute = Newtonsoft.Json.JsonConverterAttribute;
using JsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;

[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
[JsonConverter(typeof(TypedComponentConverter))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CargoComponent), typeDiscriminator: nameof(ComponentType.Cargo))]
[JsonDerivedType(typeof(ConanComponent), typeDiscriminator: nameof(ComponentType.Conan))]
[JsonDerivedType(typeof(CondaComponent), typeDiscriminator: nameof(ComponentType.Conda))]
[JsonDerivedType(typeof(DockerImageComponent), typeDiscriminator: nameof(ComponentType.DockerImage))]
[JsonDerivedType(typeof(DockerReferenceComponent), typeDiscriminator: nameof(ComponentType.DockerReference))]
[JsonDerivedType(typeof(DotNetComponent), typeDiscriminator: nameof(ComponentType.DotNet))]
[JsonDerivedType(typeof(GitComponent), typeDiscriminator: nameof(ComponentType.Git))]
[JsonDerivedType(typeof(GoComponent), typeDiscriminator: nameof(ComponentType.Go))]
[JsonDerivedType(typeof(LinuxComponent), typeDiscriminator: nameof(ComponentType.Linux))]
[JsonDerivedType(typeof(MavenComponent), typeDiscriminator: nameof(ComponentType.Maven))]
[JsonDerivedType(typeof(NpmComponent), typeDiscriminator: nameof(ComponentType.Npm))]
[JsonDerivedType(typeof(NuGetComponent), typeDiscriminator: nameof(ComponentType.NuGet))]
[JsonDerivedType(typeof(OtherComponent), typeDiscriminator: nameof(ComponentType.Other))]
[JsonDerivedType(typeof(PipComponent), typeDiscriminator: nameof(ComponentType.Pip))]
[JsonDerivedType(typeof(PodComponent), typeDiscriminator: nameof(ComponentType.Pod))]
[JsonDerivedType(typeof(RubyGemsComponent), typeDiscriminator: nameof(ComponentType.RubyGems))]
[JsonDerivedType(typeof(SpdxComponent), typeDiscriminator: nameof(ComponentType.Spdx))]
[JsonDerivedType(typeof(SwiftComponent), typeDiscriminator: nameof(ComponentType.Swift))]
[JsonDerivedType(typeof(VcpkgComponent), typeDiscriminator: nameof(ComponentType.Vcpkg))]
public abstract class TypedComponent
{
    [JsonIgnore] // Newtonsoft.Json
    [System.Text.Json.Serialization.JsonIgnore] // System.Text.Json
    private string id;

    internal TypedComponent()
    {
        // Reserved for deserialization.
    }

    /// <summary>Gets the type of the component, must be well known.</summary>
    [JsonConverter(typeof(StringEnumConverter))] // Newtonsoft.Json
    [JsonProperty("type", Order = int.MinValue)] // Newtonsoft.Json
    [System.Text.Json.Serialization.JsonIgnore] // System.Text.Json - type is handled by [JsonPolymorphic] discriminator
    public abstract ComponentType Type { get; }

    /// <summary>Gets the id of the component.</summary>
    [JsonProperty("id")] // Newtonsoft.Json
    [JsonPropertyName("id")] // System.Text.Json
    public string Id => this.id ??= this.ComputeId();

    [JsonPropertyName("packageUrl")]
    public virtual PackageURL PackageUrl { get; }

    [JsonIgnore] // Newtonsoft.Json
    [System.Text.Json.Serialization.JsonIgnore] // System.Text.Json
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
