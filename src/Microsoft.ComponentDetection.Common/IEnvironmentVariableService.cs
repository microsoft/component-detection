namespace Microsoft.ComponentDetection.Common
{
    public interface IEnvironmentVariableService
    {
        bool DoesEnvironmentVariableExist(string name);
    }
}
