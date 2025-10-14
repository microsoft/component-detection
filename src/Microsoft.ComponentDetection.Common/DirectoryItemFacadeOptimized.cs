#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System.Collections.Generic;
using System.Diagnostics;

[DebuggerDisplay("{Name}")]
public class DirectoryItemFacadeOptimized
{
    public string Name { get; set; }

    public HashSet<string> FileNames { get; set; }
}
