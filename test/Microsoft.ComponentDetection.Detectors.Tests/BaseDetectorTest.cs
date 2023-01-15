namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Diagnostics.CodeAnalysis;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.TestsUtilities;

public abstract class BaseDetectorTest<T>
    where T : FileComponentDetector
{
    [SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1401:Fields should be private",
        Justification = "Used in inheriting classes")]
    private protected readonly DetectorTestUtilityBuilder<T> detectorTestUtility;

    public BaseDetectorTest() =>
        this.detectorTestUtility = new DetectorTestUtilityBuilder<T>();
}
