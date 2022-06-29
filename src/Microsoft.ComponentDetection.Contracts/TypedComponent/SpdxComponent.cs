using System;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class SpdxComponent : TypedComponent
    {
        private SpdxComponent()
        {
            /* Reserved for deserialization */
        }

        public SpdxComponent(string spdxVersion, Uri documentNamespace, string name, string checksum,
            string rootElementId, string path)
        {
            SpdxVersion = ValidateRequiredInput(spdxVersion, nameof(SpdxVersion), nameof(ComponentType.Spdx));
            DocumentNamespace =
                ValidateRequiredInput(documentNamespace, nameof(DocumentNamespace), nameof(ComponentType.Spdx));
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Spdx));
            Checksum = ValidateRequiredInput(checksum, nameof(Checksum), nameof(ComponentType.Spdx));
            RootElementId = ValidateRequiredInput(rootElementId, nameof(RootElementId), nameof(ComponentType.Spdx));
            Path = ValidateRequiredInput(path, nameof(Path), nameof(ComponentType.Spdx));
        }

        public override ComponentType Type => ComponentType.Spdx;

        public string RootElementId { get; }

        public string Name { get; }

        public string SpdxVersion { get; }

        public Uri DocumentNamespace { get; }

        public string Checksum { get; }

        public string Path { get; }

        public override string Id => $"{Name}-{SpdxVersion}-{Checksum}";
    }
}