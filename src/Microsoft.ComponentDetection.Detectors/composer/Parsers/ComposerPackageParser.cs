namespace Microsoft.ComponentDetection.Detectors.Composer.Parsers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Composer.Contracts;
using Microsoft.Extensions.Logging;

internal class ComposerPackageParser(
    IFileUtilityService fileUtilityService,
    ComposerManifestParser manifestParser,
    JsonSerializerOptions serializerOptions,
    ILogger logger)
{
    internal async Task<ComposerLockFile?> ReadLockFileAsync(Stream stream, string location, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        try
        {
            var value = await JsonSerializer.DeserializeAsync<ComposerLockFile>(stream, serializerOptions, cancellationToken);
            if (value is not null)
            {
                return value;
            }

            manifestParser.ReportFailure(recorder, location, "Expected a JSON object.");
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Invalid Composer JSON in {Location}", location);
            recorder.RegisterPackageParseFailure(location);
        }

        return null;
    }

    internal Dictionary<string, LockedPackage> ReadLockFile(ComposerLockFile lockFile, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        var packages = new Dictionary<string, LockedPackage>();
        this.ReadPackages(lockFile.Packages, false, packages, recorder, cancellationToken);
        this.ReadPackages(lockFile.DevelopmentPackages, true, packages, recorder, cancellationToken);
        this.ReadLockAliases(lockFile.Aliases, packages, recorder, cancellationToken);
        return packages;
    }

    internal async Task<Dictionary<string, LockedPackage>?> ReadInstalledPackagesAsync(string vendorDirectory, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        var location = Path.Combine(vendorDirectory, "composer", "installed.json");
        if (!fileUtilityService.Exists(location))
        {
            return null;
        }

        ComposerInstalledPackages? inventory;
        try
        {
            using var stream = fileUtilityService.MakeFileStream(location);
            var root = await JsonSerializer.DeserializeAsync<JsonElement>(stream, serializerOptions, cancellationToken);
            inventory = root.ValueKind == JsonValueKind.Array
                ? new ComposerInstalledPackages { Packages = root.Deserialize<IReadOnlyList<ComposerPackage?>>(serializerOptions) }
                : root.Deserialize<ComposerInstalledPackages>(serializerOptions);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Invalid Composer JSON in {Location}", location);
            recorder.RegisterPackageParseFailure(location);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Unable to read Composer installed inventory at {Location}; falling back to manifest requirements.", location);
            recorder.RegisterPackageParseFailure(location);
            return null;
        }

        if (inventory is null)
        {
            manifestParser.ReportFailure(recorder, location, "Expected a JSON object or installed package array.");
            return null;
        }

        if (inventory.Packages is null)
        {
            manifestParser.ReportFailure(recorder, location, "Expected an installed package array in packages.");
            return null;
        }

        var packages = new Dictionary<string, LockedPackage>();
        this.ReadPackages(inventory.Packages, false, packages, recorder, cancellationToken);
        if (inventory.DevelopmentPackageNames is not { } names)
        {
            return packages;
        }

        var developmentPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                manifestParser.ReportFailure(recorder, $"{location}.dev-package-names", "Expected a nonempty development package name.");
                continue;
            }

            developmentPackages.Add(name.Trim());
        }

        foreach (var package in packages.Values)
        {
            package.IsDevelopment = developmentPackages.Contains(package.Component.Name.Trim());
        }

        return packages;
    }

    internal void RegisterPackages(
        Dictionary<string, LockedPackage> packages,
        ComposerManifest? manifestRoot,
        ISingleFileComponentRecorder recorder,
        CancellationToken cancellationToken)
    {
        // Names and aliases are lookup keys, never additional detected components.
        var names = packages.Values.GroupBy(package => package.Component.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var providers = CreateProviderLookup(packages);
        var rootClaims = manifestRoot is null
            ? []
            : manifestParser.ReadRootClaims(manifestRoot, recorder);
        var roots = manifestRoot is null
            ? []
            : manifestParser.ReadLinks(manifestRoot.Require, "require", recorder, "composer.json")
                .Concat(manifestParser.ReadLinks(manifestRoot.DevelopmentRequirements, "require-dev", recorder, "composer.json"))
                .Select(pair => this.Resolve(pair.Key, pair.Value, names, providers, rootClaims)?.Component.Id)
                .OfType<string>().ToHashSet();

        foreach (var package in packages.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            recorder.RegisterUsage(new DetectedComponent(package.Component), isExplicitReferencedDependency: roots.Contains(package.Component.Id), isDevelopmentDependency: package.IsDevelopment);
        }

        foreach (var package in packages.Values)
        {
            foreach (var (name, constraint) in package.Requires)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dependency = this.Resolve(name, constraint, names, providers, rootClaims);
                if (dependency is not null && dependency.Component.Id != package.Component.Id)
                {
                    recorder.RegisterUsage(new DetectedComponent(dependency.Component), parentComponentId: package.Component.Id, isDevelopmentDependency: dependency.IsDevelopment);
                }
            }
        }

        if (manifestRoot is null)
        {
            foreach (var root in ComposerGraph.FindRoots(recorder.DependencyGraph, packages.Keys, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var package = packages[root];
                recorder.RegisterUsage(new DetectedComponent(package.Component), isExplicitReferencedDependency: true, isDevelopmentDependency: package.IsDevelopment);
            }
        }
    }

    private static Dictionary<string, List<(LockedPackage Package, string Constraint)>> CreateProviderLookup(Dictionary<string, LockedPackage> packages)
    {
        var providers = new Dictionary<string, List<(LockedPackage Package, string Constraint)>>(StringComparer.Ordinal);
        foreach (var package in packages.Values)
        {
            foreach (var (name, constraint) in package.Provides.Concat(package.Replaces))
            {
                if (!providers.TryGetValue(name, out var claims))
                {
                    providers[name] = claims = [];
                }

                if (constraint == "self.version")
                {
                    claims.Add((package, package.Component.Version));
                    foreach (var alias in package.Aliases)
                    {
                        claims.Add((package, alias));
                    }
                }
                else
                {
                    claims.Add((package, constraint));
                }
            }
        }

        return providers;
    }

    private void ReadPackages(IEnumerable<ComposerPackage?>? entries, bool isDevelopment, Dictionary<string, LockedPackage> packages, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        if (entries is null)
        {
            return;
        }

        var index = 0;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var location = isDevelopment ? $"packages-dev[{index++}]" : $"packages[{index++}]";
            if (entry is null || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Version))
            {
                manifestParser.ReportFailure(recorder, location, "Expected a package with nonempty string name and version.");
                continue;
            }

            var packageName = entry.Name.Trim();
            var separator = packageName.IndexOf('/');
            if (separator <= 0 || separator == packageName.Length - 1 || separator != packageName.LastIndexOf('/'))
            {
                manifestParser.ReportFailure(recorder, location, "Expected a nonempty vendor and package name for a Composer package URL.");
                continue;
            }

            var component = new ComposerComponent(entry.Name, entry.Version);
            var package = new LockedPackage(
                component,
                isDevelopment,
                manifestParser.ReadLinks(entry.Require, "require", recorder, location),
                manifestParser.ReadLinks(entry.Provide, "provide", recorder, location),
                manifestParser.ReadLinks(entry.Replace, "replace", recorder, location),
                manifestParser.ReadBranchAliases(entry, component.Version.Trim(), recorder, location));
            if (packages.TryGetValue(component.Id, out var existing))
            {
                // A package needed in production is never development-only, irrespective of entry order.
                existing.IsDevelopment &= isDevelopment;
                this.MergeLinks(existing.Requires, package.Requires, existing.Conflicts, "require", recorder, location);
                this.MergeLinks(existing.Provides, package.Provides, existing.Conflicts, "provide", recorder, location);
                this.MergeLinks(existing.Replaces, package.Replaces, existing.Conflicts, "replace", recorder, location);
                existing.Aliases.UnionWith(package.Aliases);
            }
            else
            {
                packages.Add(component.Id, package);
            }
        }
    }

    private void ReadLockAliases(IEnumerable<ComposerAlias?>? aliases, Dictionary<string, LockedPackage> packages, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        if (aliases is null)
        {
            return;
        }

        var packagesByName = packages.Values.ToLookup(package => package.Component.Name.Trim(), StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var entry in aliases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var location = $"aliases[{index++}]";
            if (entry is null || string.IsNullOrWhiteSpace(entry.Package) ||
                string.IsNullOrWhiteSpace(entry.Version) || string.IsNullOrWhiteSpace(entry.Alias))
            {
                manifestParser.ReportFailure(recorder, location, "Expected nonempty string package, version and alias fields.");
                continue;
            }

            var matching = packagesByName[entry.Package.Trim()]
                .Where(package => ComposerVersionConstraint.VersionsEqual(package.Component.Version, entry.Version)).ToList();
            if (matching.Count == 1)
            {
                matching[0].Aliases.Add(entry.Alias.Trim());
            }
            else
            {
                logger.LogWarning("Composer alias at {Location} does not uniquely identify an installed package.", location);
            }
        }
    }

    private void MergeLinks(Dictionary<string, string> target, Dictionary<string, string> source, HashSet<string> conflicts, string section, ISingleFileComponentRecorder recorder, string location)
    {
        foreach (var (name, constraint) in source)
        {
            var key = $"{section}.{name}";
            if (conflicts.Contains(key))
            {
                continue;
            }

            if (target.TryGetValue(name, out var existing) && existing != constraint)
            {
                manifestParser.ReportFailure(recorder, $"{location}.{key}", "Duplicate locked identity declares conflicting constraints; this dependency declaration is omitted.");
                target.Remove(name);
                conflicts.Add(key);
            }
            else
            {
                target.TryAdd(name, constraint);
            }
        }
    }

    private LockedPackage? Resolve(
        string name,
        string constraint,
        Dictionary<string, List<LockedPackage>> names,
        Dictionary<string, List<(LockedPackage Package, string Constraint)>> providers,
        Dictionary<string, List<string>> rootClaims)
    {
        if (names.TryGetValue(name, out var actual))
        {
            if (actual.Count == 1)
            {
                // Resolved identities are authoritative, including branch aliases and references.
                return actual[0];
            }

            logger.LogWarning("Composer dependency {Name} has multiple resolved versions; no edge can be inferred.", name);
            return null;
        }

        if (rootClaims.TryGetValue(name, out var rootConstraints))
        {
            foreach (var rootConstraint in rootConstraints)
            {
                if (!ComposerVersionConstraint.TryIntersects(constraint, rootConstraint, out var compatible))
                {
                    logger.LogWarning("Composer root virtual dependency {Name} has an unresolved constraint; no edge can be inferred offline.", name);
                    return null;
                }

                if (compatible)
                {
                    // Root-provided virtual dependencies are not components of the locked package graph.
                    return null;
                }
            }
        }

        if (!providers.TryGetValue(name, out var claims))
        {
            logger.LogWarning("Composer dependency {Name} is not present in the resolved package inventory.", name);
            return null;
        }

        var candidates = new HashSet<LockedPackage>();
        foreach (var (package, declaredConstraint) in claims)
        {
            if (!ComposerVersionConstraint.TryIntersects(constraint, declaredConstraint, out var compatible))
            {
                logger.LogWarning("Composer virtual dependency {Name} has an unsupported constraint; provider uniqueness cannot be established.", name);
                return null;
            }

            if (compatible)
            {
                candidates.Add(package);
            }
        }

        if (candidates.Count == 1)
        {
            return candidates.Single();
        }

        logger.LogWarning("Composer virtual dependency {Name} has {Count} compatible installed providers; no edge can be inferred.", name, candidates.Count);
        return null;
    }

    internal sealed class LockedPackage(
        ComposerComponent component,
        bool isDevelopment,
        Dictionary<string, string> requires,
        Dictionary<string, string> provides,
        Dictionary<string, string> replaces,
        HashSet<string> aliases)
    {
        internal ComposerComponent Component { get; } = component;

        internal bool IsDevelopment { get; set; } = isDevelopment;

        internal Dictionary<string, string> Requires { get; } = requires;

        internal Dictionary<string, string> Provides { get; } = provides;

        internal Dictionary<string, string> Replaces { get; } = replaces;

        internal HashSet<string> Aliases { get; } = aliases;

        internal HashSet<string> Conflicts { get; } = [];
    }
}
