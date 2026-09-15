#nullable disable
namespace Microsoft.ComponentDetection.TestsUtilities;

using Microsoft.ComponentDetection.Contracts;

public abstract class BaseDetectorTest<T>
    where T : FileComponentDetector
{
    protected DetectorTestUtilityBuilder<T> DetectorTestUtility { get; } = new();
}
