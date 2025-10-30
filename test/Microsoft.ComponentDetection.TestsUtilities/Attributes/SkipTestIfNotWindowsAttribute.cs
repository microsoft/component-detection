namespace Microsoft.ComponentDetection.TestsUtilities;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed class SkipTestIfNotWindowsAttribute : TestMethodAttribute
{
    public SkipTestIfNotWindowsAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
    : base(callerFilePath, callerLineNumber)
    {
        this.CallerFilePath = callerFilePath;
        this.CallerLineNumber = callerLineNumber;
    }

    public string CallerFilePath { get; }

    public int CallerLineNumber { get; }

    public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult<TestResult[]>([
                new TestResult
                {
                    Outcome = UnitTestOutcome.Inconclusive,
                    TestFailureException = new AssertInconclusiveException($"Skipped on {RuntimeInformation.OSDescription}."),
                },
            ]);
        }

        return base.ExecuteAsync(testMethod);
    }
}
