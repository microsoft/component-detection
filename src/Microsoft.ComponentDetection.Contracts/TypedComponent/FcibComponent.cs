namespace Microsoft.ComponentDetection.Contracts.TypedComponent;

public class FcibComponent : TypedComponent
{
    private FcibComponent()
    {
        /* Reserved for deserialization */
    }

    public FcibComponent(string path, string hash = null)
    {
        this.Path = this.ValidateRequiredInput(path, nameof(this.Path), nameof(ComponentType.Fcib));
        this.Hash = hash;
    }

    public string Path { get; set; }

    public string Hash { get; set; }

    public override ComponentType Type => ComponentType.Fcib;

    protected override string ComputeId()
    {
        if (!string.IsNullOrEmpty(this.Hash))
        {
            return $"{this.Path} {this.Hash} - {this.Type}";
        }

        return $"{this.Path} - {this.Type}";
    }
}
