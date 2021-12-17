using System;
using System.Composition;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common
{
    [Export(typeof(IEnvironmentVariableService))]
    public class EnvironmentVariableService : IEnvironmentVariableService
    {
        public bool DoesEnvironmentVariableExist(string name)
        {
            var enabledVar = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrEmpty(enabledVar);
        }
    }
}
