#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;

using System;

public class Annotation
{
    public DateTime Date { get; set; }

    public string Comment { get; set; }

    public string Type { get; set; }

    public string Annotator { get; set; }
}
