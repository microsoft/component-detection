namespace Microsoft.ComponentDetection.Detectors.Npm.Contracts;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

/// <summary>
/// Converts the author field in a package.json file, which can be either a string or an object.
/// String format: "Name &lt;email&gt; (url)" where email and url are optional.
/// </summary>
public sealed partial class PackageJsonAuthorConverter : JsonConverter<PackageJsonAuthor?>
{
    // Matches: Name <email> (url) where email and url are optional
    // Examples:
    //   "John Doe"
    //   "John Doe <john@example.com>"
    //   "John Doe <john@example.com> (https://example.com)"
    //   "John Doe (https://example.com)"
    [GeneratedRegex(@"^(?<name>([^<(]+?)?)[ \t]*(?:<(?<email>([^>(]+?))>)?[ \t]*(?:\((?<url>[^)]+?)\)|$)", RegexOptions.Compiled)]
    private static partial Regex AuthorStringPattern();

    /// <inheritdoc />
    public override PackageJsonAuthor? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var authorString = reader.GetString();
            if (string.IsNullOrWhiteSpace(authorString))
            {
                return null;
            }

            var match = AuthorStringPattern().Match(authorString);
            if (!match.Success)
            {
                return null;
            }

            var name = match.Groups["name"].Value.Trim();
            var email = match.Groups["email"].Value.Trim();
            var url = match.Groups["url"].Value.Trim();

            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return new PackageJsonAuthor
            {
                Name = name,
                Email = string.IsNullOrEmpty(email) ? null : email,
                Url = string.IsNullOrEmpty(url) ? null : url,
            };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return JsonSerializer.Deserialize<PackageJsonAuthor>(ref reader, options);
        }

        // Skip unexpected token types
        reader.Skip();
        return null;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, PackageJsonAuthor? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, options);
    }
}
