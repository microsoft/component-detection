namespace Microsoft.ComponentDetection.TestsUtilities;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class SkipTestOnWindowsAttribute : TestMethodAttribute
{
    public override TestResult[] Execute(ITestMethod testMethod)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new[]
            {
                new TestResult
                {
                    Outcome = UnitTestOutcome.Inconclusive,
                    TestFailureException = new AssertInconclusiveException("Skipped on Windows."),
                },
            };
        }

        return base.Execute(testMethod);
    }
}
