namespace Microsoft.ComponentDetection.Common;

using System.IO;
using Microsoft.ComponentDetection.Contracts;

public class ComponentStream : IComponentStream
{
    public Stream Stream { get; set; }

    public string Pattern { get; set; }

    public string Location { get; set; }
}
