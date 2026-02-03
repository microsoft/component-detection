#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System;
using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

/// <summary>
/// Provides a shared mapping between component type discriminator strings and their corresponding concrete TypedComponent types.
/// This mapping is used by both Newtonsoft.Json and System.Text.Json converters for polymorphic serialization.
/// </summary>
internal static class TypedComponentMapping
{
    /// <summary>
    /// Gets the dictionary mapping type discriminator strings to their corresponding concrete types.
    /// The keys are case-insensitive to handle variations in JSON serialization.
    /// </summary>
    public static IReadOnlyDictionary<string, Type> TypeDiscriminatorToType { get; } = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
    {
        { nameof(ComponentType.Other), typeof(OtherComponent) },
        { nameof(ComponentType.NuGet), typeof(NuGetComponent) },
        { nameof(ComponentType.Npm), typeof(NpmComponent) },
        { nameof(ComponentType.Maven), typeof(MavenComponent) },
        { nameof(ComponentType.Git), typeof(GitComponent) },
        { nameof(ComponentType.RubyGems), typeof(RubyGemsComponent) },
        { nameof(ComponentType.Cargo), typeof(CargoComponent) },
        { nameof(ComponentType.Conan), typeof(ConanComponent) },
        { nameof(ComponentType.Pip), typeof(PipComponent) },
        { nameof(ComponentType.Go), typeof(GoComponent) },
        { nameof(ComponentType.DockerImage), typeof(DockerImageComponent) },
        { nameof(ComponentType.Pod), typeof(PodComponent) },
        { nameof(ComponentType.Linux), typeof(LinuxComponent) },
        { nameof(ComponentType.Conda), typeof(CondaComponent) },
        { nameof(ComponentType.DockerReference), typeof(DockerReferenceComponent) },
        { nameof(ComponentType.Vcpkg), typeof(VcpkgComponent) },
        { nameof(ComponentType.Spdx), typeof(SpdxComponent) },
        { nameof(ComponentType.DotNet), typeof(DotNetComponent) },
        { nameof(ComponentType.Swift), typeof(SwiftComponent) },
    };

    /// <summary>
    /// Tries to get the concrete type for a given type discriminator string.
    /// </summary>
    /// <param name="typeDiscriminator">The type discriminator string from JSON.</param>
    /// <param name="targetType">When successful, contains the concrete type; otherwise null.</param>
    /// <returns>True if the type discriminator was recognized; otherwise false.</returns>
    public static bool TryGetType(string typeDiscriminator, out Type targetType)
    {
        if (string.IsNullOrEmpty(typeDiscriminator))
        {
            targetType = null;
            return false;
        }

        return TypeDiscriminatorToType.TryGetValue(typeDiscriminator, out targetType);
    }
}
