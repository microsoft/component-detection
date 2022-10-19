﻿// This file was copied from the SemanticVersioning package found at https://github.com/adamreeve/semver.net.
// The range logic from SemanticVersioning is needed in the Rust detector to supplement the Semver versioning package
// that is used elsewhere in this project.
//
// This is a temporary solution, so avoid using this functionality outside of the Rust detector. The following
// issues describe the problems with the SemanticVersioning package that make it problematic to use for versioning.
// https://github.com/adamreeve/semver.net/issues/46
// https://github.com/adamreeve/semver.net/issues/47

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Semver;

namespace Microsoft.ComponentDetection.Detectors.Rust.SemVer
{
    // A version that might not have a minor or patch
    // number, for use in ranges like "^1.2" or "2.x"
    internal class PartialVersion
    {
        private static readonly Regex VersionRegex = new Regex(
            @"^
                [v=\s]*
                (\d+|[Xx\*])                      # major version
                (
                    \.
                    (\d+|[Xx\*])                  # minor version
                    (
                        \.
                        (\d+|[Xx\*])              # patch version
                        (\-?([0-9A-Za-z\-\.]+))?  # pre-release version
                        (\+([0-9A-Za-z\-\.]+))?   # build version (ignored)
                    )?
                )?
                $",
            RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public PartialVersion(string input)
        {
            string[] xValues = { "X", "x", "*" };

            if (string.IsNullOrEmpty(input.Trim()))
            {
                // Empty input means any version
                return;
            }

            var match = VersionRegex.Match(input);
            if (!match.Success)
            {
                throw new ArgumentException(string.Format("Invalid version string: \"{0}\"", input));
            }

            if (xValues.Contains(match.Groups[1].Value))
            {
                this.Major = null;
            }
            else
            {
                this.Major = int.Parse(match.Groups[1].Value);
            }

            if (match.Groups[2].Success)
            {
                if (xValues.Contains(match.Groups[3].Value))
                {
                    this.Minor = null;
                }
                else
                {
                    this.Minor = int.Parse(match.Groups[3].Value);
                }
            }

            if (match.Groups[4].Success)
            {
                if (xValues.Contains(match.Groups[5].Value))
                {
                    this.Patch = null;
                }
                else
                {
                    this.Patch = int.Parse(match.Groups[5].Value);
                }
            }

            if (match.Groups[6].Success)
            {
                this.PreRelease = match.Groups[7].Value;
            }
        }

        public int? Major { get; set; }

        public int? Minor { get; set; }

        public int? Patch { get; set; }

        public string PreRelease { get; set; }

        public SemVersion ToZeroVersion()
        {
            return new SemVersion(this.Major ?? 0, this.Minor ?? 0, this.Patch ?? 0, this.PreRelease);
        }

        public bool IsFull()
        {
            return this.Major.HasValue && this.Minor.HasValue && this.Patch.HasValue;
        }
    }
}
