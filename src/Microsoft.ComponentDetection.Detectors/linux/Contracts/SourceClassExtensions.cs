namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts;

using System.Text.Json;

/// <summary>
/// Extends the auto-generated <see cref="SourceClass"/> with a method to
/// deserialize its untyped <see cref="Metadata"/> into a
/// strongly-typed <see cref="SyftSourceMetadata"/>.
/// </summary>
public partial class SourceClass
{
    /// <summary>
    /// Deserializes the <see cref="Metadata"/> property into a <see cref="SyftSourceMetadata"/>.
    /// Returns null if <see cref="Metadata"/> is null or not a <see cref="JsonElement"/>.
    /// </summary>
    /// <returns>A deserialized <see cref="SyftSourceMetadata"/> instance, or null.</returns>
    internal SyftSourceMetadata? GetSyftSourceMetadata()
    {
        if (this.Metadata is JsonElement element)
        {
            return JsonSerializer.Deserialize<SyftSourceMetadata>(element.GetRawText());
        }

        return null;
    }
}
