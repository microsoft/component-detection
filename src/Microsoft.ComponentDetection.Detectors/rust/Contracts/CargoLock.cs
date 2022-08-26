using System.Diagnostics.CodeAnalysis;

namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Tomlyn.Model;

    // Represents Cargo.Lock file structure.
    [DataContract]
    public class CargoLock
    {
        [DataMember(Name = "package")]
        public List<CargoPackage> Package { get; set; }

        [DataMember(Name = "metadata")]
        public TomlTable Metadata { get; set; }
    }
}
