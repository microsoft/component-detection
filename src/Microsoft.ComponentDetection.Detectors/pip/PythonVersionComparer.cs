#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System.Collections.Generic;

public class PythonVersionComparer : IComparer<string>
{
    public int Compare(string x, string y)
    {
        var xVer = PythonVersion.Create(x);
        var yVer = PythonVersion.Create(y);

        return xVer.CompareTo(yVer);
    }
}
