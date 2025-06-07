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

            if (model is TomlTable table)
            {
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

                            if (pkgTable.TryGetValue("dependencies", out var depsObj) && depsObj is TomlArray depsArray)
                            {
                                foreach (var dep in depsArray)
                                {
                                    if (dep is TomlTable depTable)
                                    {
                                        depTable.TryGetValue("name", out var depNameObj);
                                        var depName = depNameObj as string;
                                        depTable.TryGetValue("specifier", out var specObj);
                                        var depSpec = specObj as string;
                                        uvPackage.Dependencies.Add(new UvDependency
                                        {
                                            Name = depName,
                                            Specifier = depSpec,
                                        });
                                    }
                                }
                            }

                            if (pkg.TryGetValue("metadata", out var metadataObj) && metadataObj is TomlTable metadataTable)
                            {
                                if (metadataTable.TryGetValue("requires-dist", out var requiresDistObj) && requiresDistObj is TomlArray requiresDistArr)
                                {
                                    foreach (var req in requiresDistArr)
                                    {
                                        if (req is TomlTable reqTable && reqTable.TryGetValue("name", out var requiresDistNameObj) && requiresDistNameObj is string requiresDistName)
                                        {
                                            var spec = reqTable.TryGetValue("specifier", out var specObj) && specObj is string s ? s : null;
                                            uvPackage.MetadataRequiresDist.Add(new UvDependency { Name = requiresDistName, Specifier = spec });
                                        }
                                    }
                                }

                                if (metadataTable.TryGetValue("requires-dev", out var requiresDevObj) && requiresDevObj is TomlTable requiresDevTable)
                                {
                                    if (requiresDevTable.TryGetValue("dev", out var devObj) && devObj is TomlArray devArr)
                                    {
                                        foreach (var req in devArr)
                                        {
                                            if (req is TomlTable reqTable && reqTable.TryGetValue("name", out var devNameObj) && devNameObj is string devDependencyName)
                                            {
                                                var spec = reqTable.TryGetValue("specifier", out var specObj) && specObj is string s ? s : null;
                                                uvPackage.MetadataRequiresDev.Add(new UvDependency { Name = devDependencyName, Specifier = spec });
                                            }
                                        }
                                    }
                                }
                            }

                            uvLock.Packages.Add(uvPackage);
                        }
                    }
                }
            }

            return uvLock;
        }
    }
}
