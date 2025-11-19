#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts;

public class Package
{
    public string SPDXID { get; set; }

    public string VersionInfo { get; set; }

    public string DownloadLocation { get; set; }

    public string Filename { get; set; }

    public string Homepage { get; set; }

    public string Description { get; set; }

    public string Name { get; set; }

    public Annotation[] Annotations { get; set; }
}
