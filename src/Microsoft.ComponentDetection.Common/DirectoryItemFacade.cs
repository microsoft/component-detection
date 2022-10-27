namespace Microsoft.ComponentDetection.Common
{
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using Microsoft.ComponentDetection.Contracts;

    [DebuggerDisplay("{Name}")]
    public class DirectoryItemFacade
    {
        public string Name { get; set; }

        public Collection<DirectoryItemFacade> Directories { get; set; }

        public Collection<IComponentStream> Files { get; set; }
    }
}
