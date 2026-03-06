namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents Linux distribution information parsed from /etc/os-release or /usr/lib/os-release.
/// </summary>
public sealed class LinuxDistribution
{
    /// <summary>
    /// Gets the lower-case operating system identifier (e.g., "ubuntu", "rhel", "fedora").
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the operating system version number or identifier.
    /// </summary>
    public string VersionId { get; init; }

    /// <summary>
    /// Gets the operating system name without version information.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets a human-readable operating system name with version.
    /// </summary>
    public string PrettyName { get; init; }

    /// <summary>
    /// Parses an os-release file content and returns a LinuxDistribution object.
    /// The os-release format is defined at https://www.freedesktop.org/software/systemd/man/os-release.html.
    /// </summary>
    /// <param name="content">The content of the os-release file.</param>
    /// <returns>A LinuxDistribution object or null if parsing fails.</returns>
    public static LinuxDistribution ParseOsRelease(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedLine = line.Trim();

            // Skip comments and empty lines
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            var parts = trimmedLine.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            // Remove quotes if present
            if (
                value.Length >= 2
                && (
                    (value.StartsWith('\"') && value.EndsWith('\"'))
                    || (value.StartsWith('\'') && value.EndsWith('\''))
                )
            )
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        // At minimum, we need an ID field
        if (!values.ContainsKey("ID"))
        {
            return null;
        }

        return new LinuxDistribution
        {
            Id = values.GetValueOrDefault("ID"),
            VersionId = values.GetValueOrDefault("VERSION_ID"),
            Name = values.GetValueOrDefault("NAME"),
            PrettyName = values.GetValueOrDefault("PRETTY_NAME"),
        };
    }
}
