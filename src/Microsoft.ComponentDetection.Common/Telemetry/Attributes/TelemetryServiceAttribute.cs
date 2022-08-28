namespace Microsoft.ComponentDetection.Common.Telemetry.Attributes
{
    using System;

    public class TelemetryServiceAttribute : Attribute
    {
        public string ServiceType { get; }

        public TelemetryServiceAttribute(string serviceType)
        {
            this.ServiceType = serviceType;
        }
    }
}
