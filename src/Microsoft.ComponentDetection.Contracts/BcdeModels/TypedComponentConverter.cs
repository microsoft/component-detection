#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System;
using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class TypedComponentConverter : JsonConverter
{
    private readonly Dictionary<ComponentType, Type> componentTypesToTypes = new Dictionary<ComponentType, Type>
    {
        { ComponentType.Other, typeof(OtherComponent) },
        { ComponentType.NuGet, typeof(NuGetComponent) },
        { ComponentType.Npm, typeof(NpmComponent) },
        { ComponentType.Maven, typeof(MavenComponent) },
        { ComponentType.Git, typeof(GitComponent) },
        { ComponentType.RubyGems, typeof(RubyGemsComponent) },
        { ComponentType.Cargo, typeof(CargoComponent) },
        { ComponentType.Conan, typeof(ConanComponent) },
        { ComponentType.Pip, typeof(PipComponent) },
        { ComponentType.Go, typeof(GoComponent) },
        { ComponentType.DockerImage, typeof(DockerImageComponent) },
        { ComponentType.Pod, typeof(PodComponent) },
        { ComponentType.Linux, typeof(LinuxComponent) },
        { ComponentType.Conda, typeof(CondaComponent) },
        { ComponentType.DockerReference, typeof(DockerReferenceComponent) },
        { ComponentType.Vcpkg, typeof(VcpkgComponent) },
        { ComponentType.Spdx, typeof(SpdxComponent) },
        { ComponentType.DotNet, typeof(DotNetComponent) },
    };

    public override bool CanWrite
    {
        get { return false; }
    }

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(TypedComponent);
    }

    public override object ReadJson(
        JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var jo = JToken.Load(reader);

        var value = (ComponentType)Enum.Parse(typeof(ComponentType), (string)jo["type"]);
        var targetType = this.componentTypesToTypes[value];
        var instanceOfTypedComponent = Activator.CreateInstance(targetType, true);
        serializer.Populate(jo.CreateReader(), instanceOfTypedComponent);

        return instanceOfTypedComponent;
    }

    public override void WriteJson(
        JsonWriter writer,
        object value,
        JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
