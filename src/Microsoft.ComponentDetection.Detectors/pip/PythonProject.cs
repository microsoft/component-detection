namespace Microsoft.ComponentDetection.Detectors.Pip
{
    using System.Collections.Generic;

    /// <summary>
    /// A project on pypi.
    /// </summary>
    public class PythonProject
    {
        public Dictionary<string, IList<PythonProjectRelease>> Releases { get; set; }
    }
}
