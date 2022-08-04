using System;

namespace Microsoft.ComponentDetection.Common.Telemetry.Attributes
{
    public class TelemetryServiceAttribute : Attribute
    {
        public string ServiceType { get; }

        public TelemetryServiceAttribute(string serviceType)
        {
            this.ServiceType = serviceType;
        }
    }
}
