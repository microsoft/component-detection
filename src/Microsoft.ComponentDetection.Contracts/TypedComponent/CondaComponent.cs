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

        public string Build { get; }

        public string Channel { get; }

        public string Name { get; }

        public string Namespace { get; }

        public string Subdir { get; }

        public string Version { get; }

        public string Url { get; }

        public string MD5 { get; }

        public override ComponentType Type => ComponentType.Conda;

        public override string Id => $"{Name} {Version} {Build} {Channel} {Subdir} {Namespace} {Url} {MD5} - {Type}";
    }
}
