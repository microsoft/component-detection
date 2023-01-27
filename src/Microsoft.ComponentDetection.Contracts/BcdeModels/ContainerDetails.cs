using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

/// <summary>
///  Details for a docker container.
/// </summary>
[JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class ContainerDetails
{
    /// <summary>
    ///  Gets or sets the ImageId for the docker container.
    /// </summary>
    public string ImageId { get; set; }

    /// <summary>
    /// Gets or sets the unique id for the container.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the digests for the container.
    /// </summary>
    public IEnumerable<string> Digests { get; set; }

    /// <summary>
    ///  Gets or sets the Repository:Tag for the base image of the docker container
    ///     ex: alpine:latest || alpine:v3.1 || mcr.microsoft.com/dotnet/sdk:5.0.
    /// </summary>
    public string BaseImageRef { get; set; }

    /// <summary>
    /// Gets or sets the digest of the exact image used as the base image. This is to avoid errors if there are ref updates between build time and scan time.
    /// </summary>
    public string BaseImageDigest { get; set; }

    /// <summary>
    /// Gets or sets the digest of the exact image used as the base image. This is to avoid errors if there are ref updates between build time and scan time.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the tags for the container.
    /// </summary>
    public IEnumerable<string> Tags { get; set; }

    /// <summary>
    /// Gets or sets the layers of the docker container.
    /// </summary>
    public IEnumerable<DockerLayer> Layers { get; set; }
}
