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

        // Metadata dependencies (requires-dist)
        public List<UvDependency> MetadataRequiresDist { get; set; } = [];

        // Metadata dev dependencies (requires-dev)
        public List<UvDependency> MetadataRequiresDev { get; set; } = [];

        // static method to parse the TOML stream into a UvLock model
        public static UvLock Parse(Stream tomlStream)
        {
            using var reader = new StreamReader(tomlStream);
            var tomlContent = reader.ReadToEnd();
            var model = Toml.ToModel(tomlContent);

            var uvLock = new UvLock();

            if (model is TomlTable table)
            {
                // add packages from the TOML model
                if (table.TryGetValue("package", out var packagesObj) && packagesObj is TomlTableArray packages)
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

                // Parse [package.metadata].requires-dist
                if (table.TryGetValue("package.metadata", out var metadataObj) && metadataObj is TomlTable metadataTable)
                {
                    if (metadataTable.TryGetValue("requires-dist", out var requiresDistObj) && requiresDistObj is TomlTableArray requiresDistArr)
                    {
                        foreach (var req in requiresDistArr)
                        {
                            if (req is TomlTable reqTable && reqTable.TryGetValue("name", out var nameObj) && nameObj is string name)
                            {
                                var spec = reqTable.TryGetValue("specifier", out var specObj) && specObj is string s ? s : null;
                                uvLock.MetadataRequiresDist.Add(new UvDependency { Name = name, Specifier = spec });
                            }
                        }
                    }
                }

                // Parse [package.metadata.requires-dev].dev
                if (table.TryGetValue("package.metadata.requires-dev", out var requiresDevObj) && requiresDevObj is TomlTable requiresDevTable)
                {
                    if (requiresDevTable.TryGetValue("dev", out var devObj) && devObj is TomlTableArray devArr)
                    {
                        foreach (var req in devArr)
                        {
                            if (req is TomlTable reqTable && reqTable.TryGetValue("name", out var nameObj) && nameObj is string name)
                            {
                                var spec = reqTable.TryGetValue("specifier", out var specObj) && specObj is string s ? s : null;
                                uvLock.MetadataRequiresDev.Add(new UvDependency { Name = name, Specifier = spec });
                            }
                        }
                    }
                }
            }

            return uvLock;
        }
    }
}
