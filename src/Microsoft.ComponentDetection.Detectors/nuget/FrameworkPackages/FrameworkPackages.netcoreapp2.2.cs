namespace Microsoft.ComponentDetection.Detectors.NuGet;

using global::NuGet.Frameworks;

/// <summary>
/// Framework packages for .NETCoreApp,Version=v2.2.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NETCoreApp22
    {
        // .NETCore 2.2 was the same as .NETCore 2.1
        internal static FrameworkPackages Instance { get; } = new(NuGetFramework.Parse("netcoreapp2.2"), "Microsoft.NETCore.App", NETCoreApp21.Instance);

        internal static void Register() => FrameworkPackages.Register(Instance);
    }
}
