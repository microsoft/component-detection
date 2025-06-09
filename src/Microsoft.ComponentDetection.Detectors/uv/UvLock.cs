namespace Microsoft.ComponentDetection.Detectors.Uv
{
    using System;
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
            return new UvLock
            {
                Packages = ParsePackagesFromModel(model),
            };
        }

        internal static List<UvPackage> ParsePackagesFromModel(object model)
        {
            if (model is not TomlTable table)
            {
                throw new InvalidOperationException("TOML root is not a table");
            }

            if (!table.TryGetValue("package", out var packagesObj) || packagesObj is not TomlTableArray packages)
            {
                return [];
            }

            var result = new List<UvPackage>();
            foreach (var pkg in packages)
            {
                var parsed = ParsePackage(pkg);
                if (parsed != null)
                {
                    result.Add(parsed);
                }
            }

            return result;
        }

        internal static UvPackage ParsePackage(object pkg)
        {
            if (pkg is not TomlTable pkgTable)
            {
                return null;
            }

            if (!pkgTable.TryGetValue("name", out var nameObj) || nameObj is not string name)
            {
                return null;
            }

            if (!pkgTable.TryGetValue("version", out var versionObj) || versionObj is not string version)
            {
                return null;
            }

            var uvPackage = new UvPackage
            {
                Name = name,
                Version = version,
                Dependencies = [],
                MetadataRequiresDist = [],
                MetadataRequiresDev = [],
            };

            if (pkgTable.TryGetValue("dependencies", out var depsObj) && depsObj is TomlArray depsArray)
            {
                uvPackage.Dependencies = ParseDependenciesArray(depsArray);
            }

            if (pkgTable.TryGetValue("metadata", out var metadataObj) && metadataObj is TomlTable metadataTable)
            {
                ParseMetadata(metadataTable, uvPackage);
            }

            return uvPackage;
        }

        internal static List<UvDependency> ParseDependenciesArray(TomlArray depsArray)
        {
            var deps = new List<UvDependency>();
            foreach (var dep in depsArray)
            {
                if (dep is TomlTable depTable)
                {
                    if (depTable.TryGetValue("name", out var depNameObj) && depNameObj is string depName)
                    {
                        var depSpec = depTable.TryGetValue("specifier", out var specObj) && specObj is string s ? s : null;
                        deps.Add(new UvDependency
                        {
                            Name = depName,
                            Specifier = depSpec,
                        });
                    }
                }
            }

            return deps;
        }

        internal static void ParseMetadata(TomlTable metadataTable, UvPackage uvPackage)
        {
            if (metadataTable.TryGetValue("requires-dist", out var requiresDistObj) && requiresDistObj is TomlArray requiresDistArr)
            {
                uvPackage.MetadataRequiresDist = ParseDependenciesArray(requiresDistArr);
            }

            if (metadataTable.TryGetValue("requires-dev", out var requiresDevObj) && requiresDevObj is TomlTable requiresDevTable)
            {
                if (requiresDevTable.TryGetValue("dev", out var devObj) && devObj is TomlArray devArr)
                {
                    uvPackage.MetadataRequiresDev = ParseDependenciesArray(devArr);
                }
            }
        }
    }
}
