#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Rust;

using System;
using System.Collections.Generic;
using Microsoft.ComponentDetection.Detectors.Rust.Contracts;

using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

public class DependencySpecification
{
    private readonly IDictionary<string, ISet<ISet<Range>>> dependencies;

    public DependencySpecification() => this.dependencies = new Dictionary<string, ISet<ISet<Range>>>();

    public void Add(string name, string cargoVersionSpecifier)
    {
        ISet<Range> ranges = new HashSet<Range>();
        var specifiers = cargoVersionSpecifier.Split([',']);
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
        if (!this.dependencies.ContainsKey(package.Name))
        {
            return false;
        }

        foreach (var ranges in this.dependencies[package.Name])
        {
            var allSatisfied = true;
            foreach (var range in ranges)
            {
                if (Version.TryParse(package.Version, out var sv))
                {
                    if (!range.IsSatisfied(sv))
                    {
                        allSatisfied = false;
                    }
                }
                else
                {
                    throw new FormatException($"Could not parse {package.Version} into a valid Semver");
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
