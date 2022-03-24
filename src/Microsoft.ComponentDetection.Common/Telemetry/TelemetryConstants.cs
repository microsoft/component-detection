using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.ComponentDetection.Orchestrator, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b101e980bad6a4194bcaf85cf037aecbe8b1fca61429ad511862c91be758742390c40ecc64c3a664103b071f6b3a563dd18c460c98f74a4fe2eaca8ca2672c777aec1a2d4672e3e4c0fb005548fe4a39c9fa48c8b6d094444dc45b02c4f9bf1fa7b3b91cdbe4921717869973a8d96f4f3a371f22ed03ff9b700f1534c014d5cb")]

namespace Microsoft.ComponentDetection.Common.Telemetry
{
    public static class TelemetryConstants
    {
        private static Guid correlationId;

        public static Guid CorrelationId
        {
            get
            {
                if (correlationId == Guid.Empty)
                {
                    correlationId = Guid.NewGuid();
                }

                return correlationId;
            }

            set // This is temporarily public, but once a process boundary exists it will be internal and initialized by Orchestrator in BCDE
            {
                correlationId = value;
            }
        }
    }
}
