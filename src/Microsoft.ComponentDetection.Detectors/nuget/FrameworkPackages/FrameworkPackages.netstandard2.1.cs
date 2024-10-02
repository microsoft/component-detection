namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

public partial class FrameworkPackages
{
    internal static NuGetFramework NetStandard21 { get; } = NuGetFramework.Parse("netstandard2.1");

    internal static FrameworkPackages NetStandard21Packages { get; } = new(NetStandard21, NetStandard20Packages)
    {
            { "System.Buffers", "4.3.0" },
            { "System.Memory", "4.3.0" },
            { "System.Numerics.Vectors", "4.3.0" },
            { "System.Reflection.DispatchProxy", "4.3.0" },
            { "System.Reflection.Emit", "4.3.0" },
            { "System.Reflection.Emit.ILGeneration", "4.3.0" },
            { "System.Reflection.Emit.Lightweight", "4.3.0" },
            { "System.Security.Principal.Windows", "4.4.0" },
            { "System.Threading.Tasks.Extensions", "4.3.0" },
    };
}
