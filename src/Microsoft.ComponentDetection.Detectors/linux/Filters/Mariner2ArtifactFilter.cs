namespace Microsoft.ComponentDetection.Detectors.Linux.Filters;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Common.Telemetry.Records;
using Microsoft.ComponentDetection.Detectors.Linux.Contracts;
using Newtonsoft.Json;

/// <summary>
/// Filters out invalid ELF binary packages from Mariner 2.0 images that lack proper release/epoch version fields.
/// This workaround addresses an issue where Syft's elf-binary-package-cataloger detects packages without complete
/// version information. The issue was fixed in Azure Linux 3.0 (https://github.com/microsoft/azurelinux/pull/10405),
/// but Mariner 2.0 no longer receives non-security updates and is deprecated as of July 2025.
/// Related Syft PR: https://github.com/anchore/syft/pull/3008.
/// </summary>
public class Mariner2ArtifactFilter : IArtifactFilter
{
    /// <inheritdoc/>
    public IEnumerable<ArtifactElement> Filter(IEnumerable<ArtifactElement> artifacts, Distro distro)
    {
        if (artifacts == null || distro == null)
        {
            return artifacts ?? [];
        }

        // Only apply this filter to Mariner 2.0
        if (distro.Id != "mariner" || distro.VersionId != "2.0")
        {
            return artifacts;
        }

        using var syftTelemetryRecord = new LinuxScannerSyftTelemetryRecord();

        var artifactsList = artifacts.ToList();

        // Find ELF packages that lack release version (indicated by missing dash in version string)
        var elfVersionsWithoutRelease = artifactsList
            .Where(artifact =>
                artifact.FoundBy == "elf-binary-package-cataloger" && // Specific cataloger with invalid results
                !artifact.Version.Contains('-', StringComparison.OrdinalIgnoreCase)) // Missing release version
            .ToList();

        if (elfVersionsWithoutRelease.Count > 0)
        {
            var removedComponents = new List<string>();
            foreach (var elfArtifact in elfVersionsWithoutRelease)
            {
                removedComponents.Add($"{elfArtifact.Name} {elfArtifact.Version}");
                artifactsList.Remove(elfArtifact);
            }

            syftTelemetryRecord.ComponentsRemoved = JsonConvert.SerializeObject(removedComponents);
        }

        return artifactsList;
    }
}
