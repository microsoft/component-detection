using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common;

[DebuggerDisplay("{Name}")]
public class DirectoryItemFacade
{
    public string Name { get; set; }

    public List<DirectoryItemFacade> Directories { get; set; }

    public List<IComponentStream> Files { get; set; }
}
