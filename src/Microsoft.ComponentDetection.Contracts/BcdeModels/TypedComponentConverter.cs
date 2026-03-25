#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class TypedComponentConverter : JsonConverter
{
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

        var typeString = (string)jo["type"];
        if (!TypedComponentMapping.TryGetType(typeString, out var targetType))
        {
            // Unknown component type - return null to allow forward compatibility
            // when new component types are added but downstream clients haven't updated yet
            return null;
        }

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
