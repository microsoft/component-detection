namespace Microsoft.ComponentDetection.Common;
using System;
using System.Composition;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;

[Export(typeof(IEnvironmentVariableService))]
public class EnvironmentVariableService : IEnvironmentVariableService
{
    public bool DoesEnvironmentVariableExist(string name)
    {
        return this.GetEnvironmentVariable(name) != null;
    }

    public string GetEnvironmentVariable(string name)
    {
        // Environment variables are case-insensitive on Windows, and case-sensitive on
        // Linux and MacOS.
        // https://docs.microsoft.com/en-us/dotnet/api/system.environment.getenvironmentvariable
        var caseInsensitiveName = Environment.GetEnvironmentVariables().Keys
            .OfType<string>()
            .FirstOrDefault(x => string.Compare(x, name, true) == 0);

        return caseInsensitiveName != null ? Environment.GetEnvironmentVariable(caseInsensitiveName) : null;
    }

    public bool IsEnvironmentVariableValueTrue(string name)
    {
        _ = bool.TryParse(this.GetEnvironmentVariable(name), out var result);
        return result;
    }
}
