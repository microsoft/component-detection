using System.Runtime.Serialization;

namespace Microsoft.ComponentDetection.Detectors.Poetry.Contracts
{
    public class PoetrySource
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }

        [DataMember(Name = "reference")]
        public string Reference { get; set; }

        [DataMember(Name = "resolved_reference")]
        public string ResolvedReference { get; set; }
    }
}
