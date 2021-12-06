using System;
using System.Composition;

namespace Microsoft.ComponentDetection.Common
{
    [Export(typeof(IEnvironmentVariableService))]
    public class EnvironmentVariableService : IEnvironmentVariableService
    {
        public bool DoesEnvironmentVariableExist(string name)
        {
            var enabledVar = Environment.GetEnvironmentVariable("EnableGoCliScanning");
            return !string.IsNullOrEmpty(enabledVar);
        }
    }
}
