namespace Microsoft.ComponentDetection.Contracts
{
    public class KillSwitchConfiguration
    {
        public bool IsDetectionStopped { get; set; }

        public string ReasonForStoppingDetection { get; set; }
    }
}
