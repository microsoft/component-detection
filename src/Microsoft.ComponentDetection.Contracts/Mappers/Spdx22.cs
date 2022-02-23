using Microsoft.ComponentDetection.Contracts.BcdeModels;

namespace Microsoft.ComponentDetection.Contracts.Mappers
{
    using CycloneDX.Spdx.Interop;
    using CycloneDX.Spdx.Models.v2_2;
    using CycloneDX.Spdx.Serialization;

    public static class Spdx22
    {
        public static string ToSpdxString(this ScanResult scanResult) => JsonSerializer.Serialize(scanResult.ToSpdx());

        public static SpdxDocument ToSpdx(this ScanResult scanResult) => scanResult.ToCycloneDx().ToSpdx();
    }
}
