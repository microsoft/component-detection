namespace Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.ComponentDetection.Contracts;

public class DetectorTestUtilityCreator
{
    public static DetectorTestUtility<T> Create<T>()
        where T : FileComponentDetector, new()
    {
        return new DetectorTestUtility<T>().WithDetector(new T());
    }
}
