namespace Microsoft.ComponentDetection.Detectors.Tests;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.TestsUtilities;

public abstract class BaseDetectorTest<T>
    where T : FileComponentDetector
{
    private protected DetectorTestUtilityBuilder<T> DetectorTestUtility { get; } = new();
}
