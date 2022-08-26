using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Microsoft.ComponentDetection.Detectors.Poetry.Contracts
{
    // Represents Poetry.Lock file structure.
    [DataContract]
    public class PoetryLock
    {
        [DataMember(Name = "Package")]
        public List<PoetryPackage> Package { get; set; }
    }
}
