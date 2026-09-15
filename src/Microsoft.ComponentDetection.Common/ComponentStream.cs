#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System.IO;
using Microsoft.ComponentDetection.Contracts;

/// <inheritdoc />
public class ComponentStream : IComponentStream
{
    /// <inheritdoc />
    public Stream Stream { get; set; }

    /// <inheritdoc />
    public string Pattern { get; set; }

    /// <inheritdoc />
    public string Location { get; set; }
}
