namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Converts the engines field in a package.json file.
/// Engines is typically an object mapping engine names to version ranges,
/// but can occasionally be an array of strings in malformed package.json files.
/// </summary>
public sealed class PackageJsonEnginesConverter : JsonConverter<IDictionary<string, string>?>
{
    /// <inheritdoc />
    public override IDictionary<string, string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var result = new Dictionary<string, string>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    if (propertyName is not null && reader.TokenType == JsonTokenType.String)
                    {
                        var value = reader.GetString();
                        if (value is not null)
                        {
                            result[propertyName] = value;
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }

            return result;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // Some malformed package.json files have engines as an array
            // We parse the array to check for known engine strings but return an empty dictionary
            // since we can't map array values to key-value pairs
            var result = new Dictionary<string, string>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();

                    // If the array contains strings like "vscode", we note it
                    // This matches the behavior of the original detector which checked for vscode engine
                    if (value is not null && value.Contains("vscode", StringComparison.OrdinalIgnoreCase))
                    {
                        result["vscode"] = value;
                    }
                }
            }

            return result;
        }

        // Skip unexpected token types
        reader.Skip();
        return null;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IDictionary<string, string>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WriteString(kvp.Key, kvp.Value);
        }

        writer.WriteEndObject();
    }
}
