namespace Microsoft.ComponentDetection.Contracts.TypedComponent
{
    public class CondaComponent : TypedComponent
    {
        private CondaComponent()
        {
            /* Reserved for deserialization */
        }

        public CondaComponent(string name, string version, string build, string channel, string subdir, string @namespace, string url, string md5)
        {
            Name = ValidateRequiredInput(name, nameof(Name), nameof(ComponentType.Conda));
            Version = ValidateRequiredInput(version, nameof(Version), nameof(ComponentType.Conda));
            Build = build;
            Channel = channel;
            Subdir = subdir;
            Namespace = @namespace;
            Url = url;
            MD5 = md5;
        }

        public string Build { get; set; }

        public string Channel { get; set; }

        public string Name { get; set; }

        public string Namespace { get; set; }

        public string Subdir { get; set; }

        public string Version { get; set; }

        public string Url { get; set; }

        public string MD5 { get; set; }

        public override ComponentType Type => ComponentType.Conda;

        public override string Id => $"{Name} {Version} {Build} {Channel} {Subdir} {Namespace} {Url} {MD5} - {Type}";
    }
}
