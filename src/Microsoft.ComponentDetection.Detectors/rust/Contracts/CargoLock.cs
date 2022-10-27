namespace Microsoft.ComponentDetection.Detectors.Rust.Contracts
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Runtime.Serialization;

    // Represents Cargo.Lock file structure.
    [DataContract]
    public class CargoLock
    {
        [DataMember(Name = "package")]
        public Collection<CargoPackage> Package { get; set; }

        [DataMember(Name = "metadata")]
        public Dictionary<string, object> Metadata { get; set; }
    }
}
