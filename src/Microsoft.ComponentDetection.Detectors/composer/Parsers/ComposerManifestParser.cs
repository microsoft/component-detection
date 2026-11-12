namespace Microsoft.ComponentDetection.Detectors.Composer.Parsers;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Composer.Contracts;
using Microsoft.Extensions.Logging;

internal class ComposerManifestParser(
    IEnvironmentVariableService environmentVariableService,
    JsonSerializerOptions serializerOptions,
    ILogger logger)
{
    internal async Task<ComposerManifest> ReadAsync(Stream stream, CancellationToken cancellationToken) =>
        await JsonSerializer.DeserializeAsync<ComposerManifest>(stream, serializerOptions, cancellationToken)
            ?? throw new JsonException("Expected a JSON object.");

    internal string GetVendorDirectory(string location, ComposerManifest? manifest)
    {
        var projectDirectory = Path.GetDirectoryName(location)!;
        var defaultVendorDirectory = Path.Combine(projectDirectory, "vendor");
        var vendorDirectory = environmentVariableService.GetEnvironmentVariable("COMPOSER_VENDOR_DIR");
        if (string.IsNullOrWhiteSpace(vendorDirectory))
        {
            vendorDirectory = "vendor";
            var configuredDirectory = manifest?.Config?.VendorDirectory;
            if (configuredDirectory is not null)
            {
                if (string.IsNullOrWhiteSpace(configuredDirectory))
                {
                    logger.LogWarning("Invalid Composer config.vendor-dir at {Location}; using the default vendor directory.", location);
                    return defaultVendorDirectory;
                }

                vendorDirectory = configuredDirectory;
            }
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(vendorDirectory, projectDirectory));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            logger.LogWarning(exception, "Invalid Composer vendor directory at {Location}; using the default vendor directory.", location);
            return defaultVendorDirectory;
        }
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Composer package lookup names require lowercase.")]
    internal void RegisterRequirements(ComposerManifest root, ISingleFileComponentRecorder recorder, string location, CancellationToken cancellationToken)
    {
        foreach (var (declarations, section, isDevelopment) in new[] { (root.Require, "require", false), (root.DevelopmentRequirements, "require-dev", true) })
        {
            var requirements = this.ReadLinks(declarations, section, recorder, location);
            if (requirements.Count == 0)
            {
                continue;
            }

            foreach (var (name, _) in declarations!)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!requirements.Remove(name.Trim().ToLowerInvariant(), out var constraint))
                {
                    continue;
                }

                if (!ComposerVersionConstraint.TryGetExactVersion(constraint, out var version))
                {
                    logger.LogWarning(
                        "Composer dependency {Name} at {Location} has unresolved constraint {Constraint}; this declaration is omitted. Provide composer.lock, installed.json, or an exact version to improve accuracy.",
                        name,
                        location,
                        constraint);
                    continue;
                }

                recorder.RegisterUsage(
                    new DetectedComponent(new ComposerComponent(name, version)),
                    isExplicitReferencedDependency: true,
                    isDevelopmentDependency: isDevelopment);
            }
        }
    }

    internal Dictionary<string, List<string>> ReadRootClaims(ComposerManifest root, ISingleFileComponentRecorder recorder)
    {
        var claims = this.ReadLinks(root.Provide, "provide", recorder, "composer.json")
            .Concat(this.ReadLinks(root.Replace, "replace", recorder, "composer.json"))
            .GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Select(pair => pair.Value).ToList());
        if (claims.Values.Any(constraints => constraints.Contains("self.version")) &&
            !string.IsNullOrWhiteSpace(root.Version))
        {
            var rootVersion = root.Version.Trim();
            var versions = this.ReadBranchAliases(root, rootVersion, recorder, "composer.json");
            versions.Add(rootVersion);
            foreach (var name in claims.Keys.ToList())
            {
                claims[name] = claims[name].SelectMany(constraint => constraint == "self.version" ? versions : [constraint]).ToList();
            }
        }

        // Without a declared root version, self.version cannot be inferred offline from Git.
        // Leave it unresolved so resolution declines to guess an edge to an installed provider.
        return claims;
    }

    internal HashSet<string> ReadBranchAliases(ComposerPackage entry, string version, ISingleFileComponentRecorder recorder, string location)
    {
        var declarations = entry.Extra?.BranchAliases;
        if (declarations is null)
        {
            return [];
        }

        var aliasLocation = $"{location}.extra.branch-alias";
        if (declarations.TryGetValue(version, out var aliasValue))
        {
            if (!string.IsNullOrWhiteSpace(aliasValue))
            {
                return [aliasValue.Trim()];
            }

            this.ReportFailure(recorder, $"{aliasLocation}.{version}", "Expected a nonempty branch alias.");
        }

        return [];
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Composer package lookup names require lowercase.")]
    internal Dictionary<string, string> ReadLinks(Dictionary<string, string?>? declarations, string section, ISingleFileComponentRecorder recorder, string location)
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        if (declarations is null)
        {
            return links;
        }

        foreach (var (rawName, rawConstraint) in declarations)
        {
            var name = rawName.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(rawConstraint))
            {
                this.ReportFailure(recorder, $"{location}.{section}.{name}", "Expected a nonempty version constraint.");
                continue;
            }

            // php, ext-*, lib-* and composer-* platform requirements do not have a vendor namespace.
            // Names such as composer/installers and ext-vendor/package are real packages, not platforms.
            if (!name.Contains('/', StringComparison.Ordinal))
            {
                logger.LogDebug(
                    "Skipping Composer dependency {Name} at {Location} without a vendor namespace; it may be a platform requirement.",
                    name,
                    $"{location}.{section}");
                continue;
            }

            if (name.StartsWith('/') || name.EndsWith('/') || name.IndexOf('/') != name.LastIndexOf('/'))
            {
                this.ReportFailure(recorder, $"{location}.{section}.{name}", "Expected a nonempty vendor and package name for a Composer package URL.");
                continue;
            }

            var constraint = rawConstraint.Trim();
            if (conflicts.Contains(name))
            {
                continue;
            }

            if (links.TryGetValue(name, out var previous) && previous != constraint)
            {
                this.ReportFailure(recorder, $"{location}.{section}.{name}", "Duplicate dependency name declares conflicting constraints; the declaration is omitted.");
                links.Remove(name);
                conflicts.Add(name);
            }
            else
            {
                links[name] = constraint;
            }
        }

        return links;
    }

    internal void ReportFailure(ISingleFileComponentRecorder recorder, string location, string reason)
    {
        logger.LogError("Malformed Composer data at {Location}: {Reason}", location, reason);
        recorder.RegisterPackageParseFailure(location);
    }
}
