namespace Microsoft.ComponentDetection.Detectors.Uv
{
    using System.Collections.Generic;
    using System.IO;
    using Tomlyn;
    using Tomlyn.Model;

    public class UvLock
    {
        // a list of packages with their dependencies
        public List<UvPackage> Packages { get; set; } = [];

        // static method to parse the TOML stream into a UvLock model
        public static UvLock Parse(Stream tomlStream)
        {
            using var reader = new StreamReader(tomlStream);
            var tomlContent = reader.ReadToEnd();
            var model = Toml.ToModel(tomlContent);

            var uvLock = new UvLock();

            // add packages from the TOML model
            if (model is TomlTable table && table.TryGetValue("package", out var packagesObj) && packagesObj is TomlTableArray packages)
            {
                foreach (var pkg in packages)
                {
                    if (pkg is TomlTable pkgTable && pkgTable.TryGetValue("name", out var nameObj) && nameObj is string name && pkgTable.TryGetValue("version", out var versionObj) && versionObj is string version)
                    {
                        var uvPackage = new UvPackage
                        {
                            Name = name,
                            Version = version,
                            Dependencies = [],
                        };

                        // Parse dependencies if present
                        if (pkgTable.TryGetValue("dependencies", out var depsObj) && depsObj is TomlTableArray depsArray)
                        {
                            foreach (var dep in depsArray)
                            {
                                if (dep is TomlTable depTable && depTable.TryGetValue("name", out var depNameObj) && depNameObj is string depName)
                                {
                                    var depSpec = depTable.TryGetValue("specifier", out var specObj) && specObj is string spec ? spec : null;
                                    uvPackage.Dependencies.Add(new UvDependency
                                    {
                                        Name = depName,
                                        Specifier = depSpec,
                                    });
                                }
                            }
                        }

                        uvLock.Packages.Add(uvPackage);
                    }
                }
            }

            return uvLock;
        }
    }
}
