namespace Microsoft.ComponentDetection.Contracts
{
    public interface IEnvironmentVariableService
    {
        bool DoesEnvironmentVariableExist(string name);

        string GetEnvironmentVariable(string name);
    }
}
