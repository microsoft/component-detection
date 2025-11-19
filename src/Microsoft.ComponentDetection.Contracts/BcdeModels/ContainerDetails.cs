#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

// Summary:
//     Details for a docker container
[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class ContainerDetails
{
    // Summary:
    //     ImageId for the docker container.
    public string ImageId { get; set; }

    // Summary:
    //     Unique id for the container.
    public int Id { get; set; }

    // Summary:
    //     Digests for the container
    public IEnumerable<string> Digests { get; set; }

    // Summary:
    //     The Repository:Tag for the base image of the docker container
    //     ex: alpine:latest || alpine:v3.1 || mcr.microsoft.com/dotnet/sdk:5.0
    public string BaseImageRef { get; set; }

    // Summary:
    //     The digest of the exact image used as the base image
    //     This is to avoid errors if there are ref updates between build time and scan time
    public string BaseImageDigest { get; set; }

    // Summary:
    //     The time the container was created
    public DateTime CreatedAt { get; set; }

    // Summary:
    //     Tags for the container
    public IEnumerable<string> Tags { get; set; }

    public IEnumerable<DockerLayer> Layers { get; set; }
}
