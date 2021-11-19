namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public class Metadata
    {
        public string Architecture { get; set; }

        public string Description { get; set; }

        public File[] Files { get; set; }

        public string GitCommitOfApkPort { get; set; }

        public long? InstalledSize { get; set; }

        public string License { get; set; }

        public string Maintainer { get; set; }

        public string OriginPackage { get; set; }

        public string Package { get; set; }

        public string PullChecksum { get; set; }

        public string PullDependencies { get; set; }

        public long? Size { get; set; }

        public string Url { get; set; }

        public string Version { get; set; }

        public string Checksum { get; set; }

        public string[] Dependencies { get; set; }

        public string Name { get; set; }

        public string Source { get; set; }

        public string SourceVersion { get; set; }

        public string[] Authors { get; set; }

        public string Homepage { get; set; }

        public string[] Licenses { get; set; }

        public JavaManifest Manifest { get; set; }

        public PomProperties PomProperties { get; set; }

        public string VirtualPath { get; set; }

        public string Author { get; set; }

        public string AuthorEmail { get; set; }

        public string Platform { get; set; }

        public string SitePackagesRootPath { get; set; }

        public string[] TopLevelPackages { get; set; }

        public long? Epoch { get; set; }

        public string Release { get; set; }

        public string SourceRpm { get; set; }

        public string Vendor { get; set; }
    }
}
