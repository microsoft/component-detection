using System.IO;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common;

public class ComponentStream : IComponentStream
{
    public Stream Stream { get; set; }

    public string Pattern { get; set; }

    public string Location { get; set; }
}
