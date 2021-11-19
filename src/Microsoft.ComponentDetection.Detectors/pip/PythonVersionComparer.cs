using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Pip
{
    public class PythonVersionComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            PythonVersion xVer = new PythonVersion(x);
            PythonVersion yVer = new PythonVersion(y);

            return xVer.CompareTo(yVer);
        }
    }
}