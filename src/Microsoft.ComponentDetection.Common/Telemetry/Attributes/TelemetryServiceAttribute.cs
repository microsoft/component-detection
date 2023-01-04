using System;

namespace Microsoft.ComponentDetection.Common.Telemetry.Attributes
{
    public class TelemetryServiceAttribute : Attribute
    {
        public TelemetryServiceAttribute(string serviceType) => this.ServiceType = serviceType;

        public string ServiceType { get; }
    }
}
