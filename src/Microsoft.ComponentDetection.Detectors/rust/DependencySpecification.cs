using System;
using System.Collections.Generic;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
using Semver;

using Range = Microsoft.ComponentDetection.Detectors.Rust.SemVer.Range;

namespace Microsoft.ComponentDetection.Detectors.Rust
{
    public class DependencySpecification
    {
        private IDictionary<string, ISet<ISet<Range>>> dependencies;

        public DependencySpecification()
        {
            dependencies = new Dictionary<string, ISet<ISet<Range>>>();
        }

        public void Add(string name, string cargoVersionSpecifier)
        {
            ISet<Range> ranges = new HashSet<Range>();
            var specifiers = cargoVersionSpecifier.Split(new char[] { ',' });
            foreach (var specifier in specifiers)
            {
                ranges.Add(new Range(specifier.Trim()));
            }

            if (!dependencies.ContainsKey(name))
            {
                dependencies.Add(name, new HashSet<ISet<Range>>());
            }

            dependencies[name].Add(ranges);
        }

        public bool MatchesPackage(CargoPackage package)
        {
            if (!dependencies.ContainsKey(package.name))
            {
                return false;
            }

            foreach (var ranges in dependencies[package.name])
            {
                var allSatisfied = true;
                foreach (var range in ranges)
                {
                    if (SemVersion.TryParse(package.version, out SemVersion sv))
                    {
                        if (!range.IsSatisfied(sv))
                        {
                            allSatisfied = false;
                        }
                    }
                    else
                    {
                        throw new FormatException($"Could not parse {package.version} into a valid Semver");
                    }
                }

                if (allSatisfied)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
