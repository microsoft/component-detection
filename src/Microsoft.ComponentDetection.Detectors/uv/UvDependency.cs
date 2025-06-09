#nullable enable
namespace Microsoft.ComponentDetection.Detectors.Uv
{
    public class UvDependency
    {
        public required string Name { get; init; }

        public string? Specifier { get; set; }
    }
}
