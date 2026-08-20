#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Text.Json.Serialization;

public class DockerLayer
{
    // Summary:
    //     the command/script that was executed in order to create the layer.
    [JsonPropertyName("CreatedBy")]
    public string CreatedBy { get; set; }

    // Summary:
    // The Layer hash (docker inspect) that represents the changes between this layer and the previous layer
    [JsonPropertyName("DiffId")]
    public string DiffId { get; set; }

    // Summary:
    //     Whether or not this layer was found in the base image of the container
    [JsonPropertyName("IsBaseImage")]
    public bool IsBaseImage { get; set; }

    // Summary:
    //     0-indexed monotonically increasing ID for the order of the layer in the container.
    //     Note: only includes non-empty layers
    [JsonPropertyName("LayerIndex")]
    public int LayerIndex { get; set; }
}
