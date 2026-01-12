#nullable disable
namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using PackageUrl;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CargoComponent), typeDiscriminator: nameof(ComponentType.Cargo))]
[JsonDerivedType(typeof(ConanComponent), typeDiscriminator: nameof(ComponentType.Conan))]
[JsonDerivedType(typeof(CondaComponent), typeDiscriminator: nameof(ComponentType.Conda))]
[JsonDerivedType(typeof(CppSdkComponent), typeDiscriminator: nameof(ComponentType.CppSdk))]
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
    [JsonIgnore]
    private string id;

    internal TypedComponent()
    {
        // Reserved for deserialization.
    }

    /// <summary>Gets the type of the component, must be well known.</summary>
    [JsonIgnore] // type is handled by [JsonPolymorphic] discriminator
    public abstract ComponentType Type { get; }

    /// <summary>Gets the id of the component.</summary>
    [JsonPropertyName("id")]
    public string Id => this.id ??= this.ComputeId();

    [JsonPropertyName("packageUrl")]
    public virtual PackageURL PackageUrl { get; }

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

    protected abstract string ComputeId();
}
