using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Tomlyn.Model;

namespace Microsoft.ComponentDetection.Detectors.Poetry.Contracts
{
    [DataContract]
    public class PoetryPackage
    {
        [DataMember(Name = "category")]
        public string Category { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "version")]
        public string Version { get; set; }

        [DataMember(Name = "source")]
        public PoetrySource Source { get; set; }

        [DataMember(Name = "dependencies")]
        public TomlTable Dependencies { get; set; }

        [DataMember(Name = "extras")]
        public TomlTable Extras { get; set; }
    }
}
