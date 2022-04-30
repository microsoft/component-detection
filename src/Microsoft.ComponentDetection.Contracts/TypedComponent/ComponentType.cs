using System.Runtime.Serialization;

namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    // This is used in BcdeModels as well
    [DataContract]
    public enum ComponentType : byte
    {
        [EnumMember]
        Other = 0,

        [EnumMember]
        NuGet = 1,

        [EnumMember]
        Npm = 2,

        [EnumMember]
        Maven = 3,

        [EnumMember]
        Git = 4,

        [EnumMember]
        RubyGems = 6,

        [EnumMember]
        Cargo = 7,

        [EnumMember]
        Pip = 8,

        [EnumMember]
        Go = 9,

        [EnumMember]
        DockerImage = 10,

        [EnumMember]
        Pod = 11,

        [EnumMember]
        Linux = 12,

        [EnumMember]
        Conda = 13,

        [EnumMember]
        Spdx = 14,

        [EnumMember]
        Vcpkg = 15,

        [EnumMember]
        Dockerfile = 16,
    }
}