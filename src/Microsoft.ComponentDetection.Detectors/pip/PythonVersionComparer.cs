namespace Microsoft.ComponentDetection.Detectors.Pip;
using System.Collections.Generic;

public class PythonVersionComparer : IComparer<string>
{
    public int Compare(string x, string y)
    {
        var xVer = new PythonVersion(x);
        var yVer = new PythonVersion(y);

        return xVer.CompareTo(yVer);
    }
}
