// Take schema from https://github.com/anchore/syft/tree/main/schema/json.
// Match version to tag used i.e. https://github.com/anchore/syft/blob/v0.16.1/internal/constants.go#L9
// Can convert JSON Schema to C# using quicktype.io.
// (change name of top Coordinate class to SyftOutput)
// <auto-generated />
using System;
using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public partial class SyftOutput
    {
        public Relationship[] ArtifactRelationships { get; set; }
        public Package[] Artifacts { get; set; }
        public Descriptor Descriptor { get; set; }
        public LinuxRelease Distro { get; set; }
        public File[] Files { get; set; }
        public Schema Schema { get; set; }
        public Secrets[] Secrets { get; set; }
        public Source Source { get; set; }
    }

    public partial class Relationship
    {
        public string Child { get; set; }
        public ConfigurationUnion? Metadata { get; set; }
        public string Parent { get; set; }
        public string Type { get; set; }
    }

    public partial class Package
    {
        public string[] Cpes { get; set; }
        public string FoundBy { get; set; }
        public string Id { get; set; }
        public string Language { get; set; }
        public string[] Licenses { get; set; }
        public Coordinates[] Locations { get; set; }
        public Metadata Metadata { get; set; }
        public string MetadataType { get; set; }
        public string Name { get; set; }
        public string Purl { get; set; }
        public string Type { get; set; }
        public string Version { get; set; }
    }

    public partial class Coordinates
    {
        public string LayerId { get; set; }
        public string Path { get; set; }
    }

    public partial class Metadata
    {
        public string Architecture { get; set; }
        public AlpmFileRecord[] Backup { get; set; }
        public string Basepackage { get; set; }
        public string Description { get; set; }
        public FileUnion[] Files { get; set; }
        public License? License { get; set; }
        public string Package { get; set; }
        public string Packager { get; set; }
        public long? Reason { get; set; }
        public long? Size { get; set; }
        public string Url { get; set; }
        public string Validation { get; set; }
        public string Version { get; set; }
        public string GitCommitOfApkPort { get; set; }
        public long? InstalledSize { get; set; }
        public string Maintainer { get; set; }
        public string OriginPackage { get; set; }
        public string PullChecksum { get; set; }
        public string PullDependencies { get; set; }
        public string Checksum { get; set; }
        public string[] Dependencies { get; set; }
        public string Name { get; set; }
        public SourceUnion? Source { get; set; }
        public string HostedUrl { get; set; }
        public string VcsUrl { get; set; }
        public string HashPath { get; set; }
        public string Path { get; set; }
        public string Sha512 { get; set; }
        public string SourceVersion { get; set; }
        public Author[] Authors { get; set; }
        public string Homepage { get; set; }
        public string[] Licenses { get; set; }
        public Dictionary<string, string> GoBuildSettings { get; set; }
        public string GoCompiledVersion { get; set; }
        public string H1Digest { get; set; }
        public string MainModule { get; set; }
        public Digest[] Digest { get; set; }
        public JavaManifest Manifest { get; set; }
        public PomProject PomProject { get; set; }
        public PomProperties PomProperties { get; set; }
        public string VirtualPath { get; set; }
        public string Author { get; set; }
        public string[] Bin { get; set; }
        public PhpComposerExternalReference Dist { get; set; }
        public string[] Keywords { get; set; }
        public string NotificationUrl { get; set; }
        public Dictionary<string, string> Provide { get; set; }
        public Dictionary<string, string> Require { get; set; }
        public Dictionary<string, string> RequireDev { get; set; }
        public Dictionary<string, string> Suggest { get; set; }
        public string Time { get; set; }
        public string Type { get; set; }
        public string AuthorEmail { get; set; }
        public PythonDirectUrlOriginInfo DirectUrlOrigin { get; set; }
        public string Platform { get; set; }
        public string SitePackagesRootPath { get; set; }
        public string[] TopLevelPackages { get; set; }
        public long? Epoch { get; set; }
        public string Release { get; set; }
        public string SourceRpm { get; set; }
        public string Vendor { get; set; }
    }

    public partial class PhpComposerAuthors
    {
        public string Email { get; set; }
        public string Homepage { get; set; }
        public string Name { get; set; }
    }

    public partial class AlpmFileRecord
    {
        public Digest[] Digest { get; set; }
        public string Gid { get; set; }
        public string Link { get; set; }
        public string Path { get; set; }
        public string Size { get; set; }
        public DateTimeOffset? Time { get; set; }
        public string Type { get; set; }
        public string Uid { get; set; }
    }

    public partial class Digest
    {
        public string Algorithm { get; set; }
        public string Value { get; set; }
    }

    public partial class PythonDirectUrlOriginInfo
    {
        public string CommitId { get; set; }
        public string Url { get; set; }
        public string Vcs { get; set; }
    }

    public partial class PhpComposerExternalReference
    {
        public string Reference { get; set; }
        public string Shasum { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
    }

    public partial class FileRecord
    {
        public DigestUnion? Digest { get; set; }
        public string Gid { get; set; }
        public string Link { get; set; }
        public string Path { get; set; }
        public Size? Size { get; set; }
        public DateTimeOffset? Time { get; set; }
        public string Type { get; set; }
        public string Uid { get; set; }
        public string OwnerGid { get; set; }
        public string OwnerUid { get; set; }
        public string Permissions { get; set; }
        public bool? IsConfigFile { get; set; }
        public string Flags { get; set; }
        public string GroupName { get; set; }
        public long? Mode { get; set; }
        public string UserName { get; set; }
    }

    public partial class PurpleDigest
    {
        public string Algorithm { get; set; }
        public string Value { get; set; }
    }

    public partial class JavaManifest
    {
        public Dictionary<string, string> Main { get; set; }
        public Dictionary<string, Dictionary<string, string>> NamedSections { get; set; }
    }

    public partial class PomProject
    {
        public string ArtifactId { get; set; }
        public string Description { get; set; }
        public string GroupId { get; set; }
        public string Name { get; set; }
        public PomParent Parent { get; set; }
        public string Path { get; set; }
        public string Url { get; set; }
        public string Version { get; set; }
    }

    public partial class PomParent
    {
        public string ArtifactId { get; set; }
        public string GroupId { get; set; }
        public string Version { get; set; }
    }

    public partial class PomProperties
    {
        public string ArtifactId { get; set; }
        public Dictionary<string, string> ExtraFields { get; set; }
        public string GroupId { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Version { get; set; }
    }

    public partial class Descriptor
    {
        public ConfigurationUnion? Configuration { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
    }

    public partial class LinuxRelease
    {
        public string BugReportUrl { get; set; }
        public string BuildId { get; set; }
        public string CpeName { get; set; }
        public string HomeUrl { get; set; }
        public string Id { get; set; }
        public string[] IdLike { get; set; }
        public string ImageId { get; set; }
        public string ImageVersion { get; set; }
        public string Name { get; set; }
        public string PrettyName { get; set; }
        public string PrivacyPolicyUrl { get; set; }
        public string SupportUrl { get; set; }
        public string Variant { get; set; }
        public string VariantId { get; set; }
        public string Version { get; set; }
        public string VersionCodename { get; set; }
        public string VersionId { get; set; }
    }

    public partial class File
    {
        public Classification[] Classifications { get; set; }
        public string Contents { get; set; }
        public Digest[] Digests { get; set; }
        public string Id { get; set; }
        public Coordinates Location { get; set; }
        public FileMetadataEntry Metadata { get; set; }
    }

    public partial class Classification
    {
        public string Class { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }

    public partial class FileMetadataEntry
    {
        public long GroupId { get; set; }
        public string LinkDestination { get; set; }
        public string MimeType { get; set; }
        public long Mode { get; set; }
        public string Type { get; set; }
        public long UserId { get; set; }
    }

    public partial class Schema
    {
        public string Url { get; set; }
        public string Version { get; set; }
    }

    public partial class Secrets
    {
        public Coordinates Location { get; set; }
        public SearchResult[] SecretsSecrets { get; set; }
    }

    public partial class SearchResult
    {
        public string Classification { get; set; }
        public long Length { get; set; }
        public long LineNumber { get; set; }
        public long LineOffset { get; set; }
        public long SeekPosition { get; set; }
        public string Value { get; set; }
    }

    public partial class Source
    {
        public ConfigurationUnion Target { get; set; }
        public string Type { get; set; }
    }

    public partial struct ConfigurationUnion
    {
        public object[] AnythingArray;
        public Dictionary<string, object> AnythingMap;
        public bool? Bool;
        public double? Double;
        public long? Integer;
        public string String;

        public static implicit operator ConfigurationUnion(object[] AnythingArray) => new ConfigurationUnion { AnythingArray = AnythingArray };
        public static implicit operator ConfigurationUnion(Dictionary<string, object> AnythingMap) => new ConfigurationUnion { AnythingMap = AnythingMap };
        public static implicit operator ConfigurationUnion(bool Bool) => new ConfigurationUnion { Bool = Bool };
        public static implicit operator ConfigurationUnion(double Double) => new ConfigurationUnion { Double = Double };
        public static implicit operator ConfigurationUnion(long Integer) => new ConfigurationUnion { Integer = Integer };
        public static implicit operator ConfigurationUnion(string String) => new ConfigurationUnion { String = String };
        public bool IsNull => this.AnythingArray == null && this.Bool == null && this.Double == null && this.Integer == null && this.AnythingMap == null && this.String == null;
    }

    public partial struct Author
    {
        public PhpComposerAuthors PhpComposerAuthors;
        public string String;

        public static implicit operator Author(PhpComposerAuthors PhpComposerAuthors) => new Author { PhpComposerAuthors = PhpComposerAuthors };
        public static implicit operator Author(string String) => new Author { String = String };
    }

    public partial struct DigestUnion
    {
        public Digest[] DigestArray;
        public PurpleDigest PurpleDigest;

        public static implicit operator DigestUnion(Digest[] DigestArray) => new DigestUnion { DigestArray = DigestArray };
        public static implicit operator DigestUnion(PurpleDigest PurpleDigest) => new DigestUnion { PurpleDigest = PurpleDigest };
    }

    public partial struct Size
    {
        public long? Integer;
        public string String;

        public static implicit operator Size(long Integer) => new Size { Integer = Integer };
        public static implicit operator Size(string String) => new Size { String = String };
    }

    public partial struct FileUnion
    {
        public FileRecord FileRecord;
        public string String;

        public static implicit operator FileUnion(FileRecord FileRecord) => new FileUnion { FileRecord = FileRecord };
        public static implicit operator FileUnion(string String) => new FileUnion { String = String };
    }

    public partial struct License
    {
        public string String;
        public string[] StringArray;

        public static implicit operator License(string String) => new License { String = String };
        public static implicit operator License(string[] StringArray) => new License { StringArray = StringArray };
    }

    public partial struct SourceUnion
    {
        public PhpComposerExternalReference PhpComposerExternalReference;
        public string String;

        public static implicit operator SourceUnion(PhpComposerExternalReference PhpComposerExternalReference) => new SourceUnion { PhpComposerExternalReference = PhpComposerExternalReference };
        public static implicit operator SourceUnion(string String) => new SourceUnion { String = String };
    }
}
