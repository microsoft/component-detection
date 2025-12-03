namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Converts the workspaces field in a package.json file.
/// Workspaces can be:
/// - An array of glob patterns: ["packages/*"]
/// - An object with a packages field: { "packages": ["packages/*"] }.
/// </summary>
public sealed class PackageJsonWorkspacesConverter : JsonConverter<IList<string>?>
{
    /// <inheritdoc />
    public override IList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var result = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (value is not null)
                    {
                        result.Add(value);
                    }
                }
            }

            return result;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            // Parse object and look for "packages" field
            IList<string>? packages = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    if (string.Equals(propertyName, "packages", StringComparison.OrdinalIgnoreCase) &&
                        reader.TokenType == JsonTokenType.StartArray)
                    {
                        packages = [];
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                var value = reader.GetString();
                                if (value is not null)
                                {
                                    packages.Add(value);
                                }
                            }
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }

            return packages;
        }

        // Skip unexpected token types
        reader.Skip();
        return null;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IList<string>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
