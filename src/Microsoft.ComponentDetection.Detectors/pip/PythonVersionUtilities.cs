#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;
using System.Linq;

public static class PythonVersionUtilities
{
    /// <summary>
    /// Determine if the version is valid for all specs.
    /// </summary>
    /// <param name="version">version.</param>
    /// <param name="specs">version specifications.</param>
    /// <returns>True if the version is valid for all specs, otherwise false. </returns>
    /// <exception cref="ArgumentException">The version or any of the specs are an invalid python version.</exception>
    public static bool VersionValidForSpec(string version, IList<string> specs)
    {
        foreach (var spec in specs)
        {
            if (!VersionValidForSpec(version, spec))
            {
                return false;
            }
        }

        return true;
    }

    // Todo, remove this code once * parsing is handled in the python version class
    public static bool CheckEquality(string version, string specVer, bool fuzzy = false)
    {
        // This handles locked prerelease versions and non *
        if (string.Equals(version, specVer, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var i = 0;
        var splitVersion = version.Split('.');
        var splitSpecVer = specVer.Split('.');

        while (true)
        {
            if (fuzzy && i == (splitSpecVer.Length - 1))
            {
                // Fuzzy matching excludes everything after first two
                return splitVersion.Length > i && int.TryParse(splitVersion[i], out var lVer) && int.TryParse(splitSpecVer[i], out var rVer) && lVer >= rVer;
            }

            // If we got here, we have an * terminator to our spec ver, so anything is fair game
            if (splitSpecVer.Length > i && splitSpecVer[i] == "*")
            {
                return true;
            }

            if (splitSpecVer.Length > i && splitVersion.Length > i)
            {
                if (string.Equals(splitSpecVer[i], splitVersion[i], StringComparison.OrdinalIgnoreCase))
                {
                    // Match keep going
                    i++;
                    continue;
                }
                else
                {
                    return false; // No match
                }
            }
            else if (i <= splitSpecVer.Length && i <= splitVersion.Length)
            {
                return true; // We got to the end, no problems
            }
            else
            {
                // Either one string terminated early, or something didn't match
                if (fuzzy && splitVersion.Length > i && !int.TryParse(splitVersion[i], out _))
                {
                    return true;
                }

                return false;
            }
        }
    }

    private static bool VersionValidForSpec(string version, string spec)
    {
        (var op, var specVersion) = ParseSpec(spec);

        var targetVer = PythonVersion.Create(version);
        var specVer = PythonVersion.Create(specVersion);

        if (!targetVer.Valid)
        {
            throw new ArgumentException($"{version} is not a valid python version");
        }

        if (!specVer.Valid)
        {
            throw new ArgumentException($"The version specification {specVersion} is not a valid python version");
        }

        return op switch
        {
            "==" => targetVer.CompareTo(specVer) == 0,
            "===" => targetVer.CompareTo(specVer) == 0,
            "<" => specVer > targetVer,
            ">" => targetVer > specVer,
            "<=" => specVer >= targetVer,
            ">=" => targetVer >= specVer,
            "!=" => targetVer.CompareTo(specVer) != 0,
            "~=" => CheckEquality(version, specVersion, true),
            _ => false,
        };
    }

    public static (string Operator, string Version) ParseSpec(string spec)
    {
        var opChars = new char[] { '=', '<', '>', '~', '!' };
        var specArray = spec.ToCharArray();

        var i = 0;
        while (i < spec.Length && i < 3 && opChars.Contains(specArray[i]))
        {
            i++;
        }

        var op = spec[..i];
        var specVerSection = spec[i..].Trim();

        return (op, specVerSection);
    }
}
