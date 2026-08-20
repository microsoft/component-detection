namespace Microsoft.ComponentDetection.Detectors.Uv;

internal class UvDependency
{
    public required string Name { get; init; }

    public string? Specifier { get; set; }
}
