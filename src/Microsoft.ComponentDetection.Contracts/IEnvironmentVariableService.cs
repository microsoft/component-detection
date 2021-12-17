namespace Microsoft.ComponentDetection.Contracts
{
    public interface IEnvironmentVariableService
    {
        bool DoesEnvironmentVariableExist(string name);
    }
}
