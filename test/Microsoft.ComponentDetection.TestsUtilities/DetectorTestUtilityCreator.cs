using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.TestsUtilities
{
    public class DetectorTestUtilityCreator
    {
        public static DetectorTestUtility<T> Create<T>()
            where T : FileComponentDetector, new()
        {
            return new DetectorTestUtility<T>().WithDetector(new T());
        }
    }
}
