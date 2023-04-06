namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System.Collections.Generic;

internal class NugetLockfileShape
{
    public int Version { get; set; }

    public Dictionary<string, Dictionary<string, PackageShape>> Dependencies { get; set; }

    public class PackageShape
    {
        public string Type { get; set; }

        public string Resolved { get; set; }
    }
}
