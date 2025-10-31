namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

public class SensitiveBinaryDetector : TypedComponent
{
    private SensitiveBinaryDetector()
    {
        /* Reserved for deserialization */
    }

    public SensitiveBinaryDetector(string path, string format, string gitBlobSha1, string stableBinaryCorrelatingId = null)
    {
        this.Format = format;
        this.Path = this.ValidateRequiredInput(path, nameof(this.Path), nameof(ComponentType.SensitiveBinary));
        this.GitBlobSha1 = gitBlobSha1;
        this.StableBinaryCorrelatingId = stableBinaryCorrelatingId;
    }

    public string Format { get; set; }

    public string Path { get; set; }

    public string GitBlobSha1 { get; set; }

    public string StableBinaryCorrelatingId { get; set; }

    public override ComponentType Type => ComponentType.SensitiveBinary;

    protected override string ComputeId()
    {
        return $"{this.Path}  {this.GitBlobSha1}  - {this.Type}";
    }
}
