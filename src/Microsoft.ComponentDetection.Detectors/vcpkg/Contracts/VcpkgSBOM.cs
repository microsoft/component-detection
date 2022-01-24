namespace Microsoft.ComponentDetection.Detectors.Vcpkg.Contracts
{
    /// <summary>
    /// Take from https://github.com/anchore/syft/tree/main/schema/json.
    /// Match version to tag used i.e. https://github.com/anchore/syft/blob/v0.16.1/internal/constants.go#L9
    /// Can convert JSON Schema to C# using quicktype.io.
    /// </summary>
    public class VcpkgSBOM
    {
        public Package[] Packages { get; set; }

        public string Name { get; set; }
    }
}
