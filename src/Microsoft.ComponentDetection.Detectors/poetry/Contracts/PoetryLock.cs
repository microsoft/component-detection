using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace Microsoft.ComponentDetection.Detectors.Poetry.Contracts
{
    // Represents Poetry.Lock file structure.
    [DataContract]
    public class PoetryLock
    {
        [DataMember(Name = "Package")]
        public Collection<PoetryPackage> Package { get; set; }

        [DataMember(Name = "metadata")]
        public Dictionary<string, object> Metadata { get; set; }
    }
}
