using System;

namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts
{
    public class Annotation
    {
        public DateTime Date { get; set; }

        public string Comment { get; set; }

        public string Type { get; set; }

        public string Annotator { get; set; }
    }
}
