namespace Microsoft.ComponentDetection.Detectors.Rust
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ComponentDetection.Detectors.Rust.Contracts;
    using Semver;

    using Range = Microsoft.ComponentDetection.Detectors.Rust.SemVer.Range;

    public class DependencySpecification
    {
        private IDictionary<string, ISet<ISet<Range>>> dependencies;

        public DependencySpecification()
        {
            this.dependencies = new Dictionary<string, ISet<ISet<Range>>>();
        }

        public void Add(string name, string cargoVersionSpecifier)
        {
            ISet<Range> ranges = new HashSet<Range>();
            var specifiers = cargoVersionSpecifier.Split(new char[] { ',' });
            foreach (var specifier in specifiers)
            {
                ranges.Add(new Range(specifier.Trim()));
            }

            if (!this.dependencies.ContainsKey(name))
            {
                this.dependencies.Add(name, new HashSet<ISet<Range>>());
            }

            this.dependencies[name].Add(ranges);
        }

        public bool MatchesPackage(CargoPackage package)
        {
            if (!this.dependencies.ContainsKey(package.name))
            {
                return false;
            }

            foreach (var ranges in this.dependencies[package.name])
            {
                var allSatisfied = true;
                foreach (var range in ranges)
                {
                    if (SemVersion.TryParse(package.version, out var sv))
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
