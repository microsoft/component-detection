#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using TypedComponentClass = Microsoft.ComponentDetection.Contracts.TypedComponent.TypedComponent;

/// <summary>
/// A System.Text.Json converter for TypedComponent that handles unknown component types gracefully.
/// When an unknown component type is encountered, it returns null instead of throwing an exception.
/// This enables forward compatibility when new component types are added but downstream clients haven't updated yet.
/// </summary>
public class TypedComponentSystemTextJsonConverter : JsonConverter<TypedComponentClass>
{
    private const string TypePropertyNameCamelCase = "type";
    private const string TypePropertyNamePascalCase = "Type";

    public override TypedComponentClass Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // Parse the JSON into a document so we can inspect it
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Extract the type discriminator
        // Check both camelCase and PascalCase property names
        if (!root.TryGetProperty(TypePropertyNameCamelCase, out var typeProperty) &&
            !root.TryGetProperty(TypePropertyNamePascalCase, out typeProperty))
        {
            return null;
        }

        var typeDiscriminator = typeProperty.GetString();
        if (!TypedComponentMapping.TryGetType(typeDiscriminator, out var targetType))
        {
            // Unknown component type - return null for forward compatibility
            return null;
        }

        // Deserialize to the specific concrete type using default serialization
        return (TypedComponentClass)root.Deserialize(targetType, options);
    }

    public override void Write(Utf8JsonWriter writer, TypedComponentClass value, JsonSerializerOptions options)
    {
        // Serialize to a document first to get all properties
        using var doc = JsonSerializer.SerializeToDocument(value, value.GetType(), options);

        writer.WriteStartObject();

        // Write the type discriminator first (using camelCase as the standard output format)
        writer.WriteString(TypePropertyNameCamelCase, value.Type.ToString());

        // Write all other properties from the serialized document
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
