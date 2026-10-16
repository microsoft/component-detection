#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// Summary:
//     Details for a docker container
public class ContainerDetails
{
    // Summary:
    //     ImageId for the docker container.
    [JsonPropertyName("imageId")]
    public string ImageId { get; set; }

    // Summary:
    //     Unique id for the container.
    [JsonPropertyName("id")]
    public int Id { get; set; }

    // Summary:
    //     Digests for the container
    [JsonPropertyName("digests")]
    public IEnumerable<string> Digests { get; set; }

    // Summary:
    //     The Repository:Tag for the base image of the docker container
    //     ex: alpine:latest || alpine:v3.1 || mcr.microsoft.com/dotnet/sdk:5.0
    [JsonPropertyName("baseImageRef")]
    public string BaseImageRef { get; set; }

    // Summary:
    //     The digest of the exact image used as the base image
    //     This is to avoid errors if there are ref updates between build time and scan time
    [JsonPropertyName("baseImageDigest")]
    public string BaseImageDigest { get; set; }

    // Summary:
    //     The time the container was created
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    // Summary:
    //     Tags for the container
    [JsonPropertyName("tags")]
    public IEnumerable<string> Tags { get; set; }

    [JsonPropertyName("layers")]
    public IEnumerable<DockerLayer> Layers { get; set; }
}
