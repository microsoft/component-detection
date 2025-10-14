#nullable disable

// Ruby detection highlights and todos:
//
// Dependencies are "fuzzy versions":
// this in and of itself could be solved by deferring dependency resolution alone until after all components are registered.
// Different sections of Ruby's lockfile can point into other sections, and the authoritative version is not replicated across
// sections-- it's only stored in, say, the Gems section.
//
// Git components are even stranger in Ruby land:
// they have an annotation for a git component that is a "name" that has no relationship to how we normally think of
// a GitComponent (remote / version). The mapping from git component name to a GitComponent can't really be handled
// in ComponentRecorder today, because "component name" for a Git component is a Ruby specific concept.
// This could be pointing to a sideloaded storage in ComponentRecorder (e.g. a <TContext> style storage that detectors
// could use to track related state as their execution goes on).
//
// The basic approach in ruby is to do two passes:
// first, make sure you have all authoritative components, then, resolve and register all dependency relationships.
//
// If we had sideloaded state for nodes in the graph, I could see us at least being able to remove the "name" mapping from ruby.
// Deferred dependencies is a lot more complicated, you would basically need a way to set up a pointer to a component based on a mapped value
// (in this case, just component name sans version) that would be resolved in an arbitrary way after the graph writing was "done".
// I don't think this is impossible (having a custom delegate for a detector to identify and map nodes to one another seems pretty easy),
// but seems complicated.
//
// There is a possibility to use manual root detection instead of automatic:
// Gemfile.lock comes with a section called "Dependencies", in the section are listed the dependencies that the user specified in the Gemfile,
// is necessary to investigate if this section is a new adition or always has been there.
namespace Microsoft.ComponentDetection.Detectors.Ruby;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class RubyComponentDetector : FileComponentDetector
{
    private static readonly Regex HeadingRegex = new Regex("^[A-Z ]+$", RegexOptions.Compiled);
    private static readonly Regex DependencyDefinitionRegex = new Regex("^ {4}[A-Za-z-]+", RegexOptions.Compiled);
    private static readonly Regex SubDependencyRegex = new Regex("^ {6}[A-Za-z-]+", RegexOptions.Compiled);

    public RubyComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<RubyComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    private enum SectionType
    {
        GEM,
        GIT,
        PATH,
    }

    public override string Id { get; } = "Ruby";

    public override IEnumerable<string> Categories => [Enum.GetName(typeof(DetectorClass), DetectorClass.RubyGems)];

    public override IList<string> SearchPatterns { get; } = ["Gemfile.lock"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.RubyGems];

    public override int Version { get; } = 3;

    public override bool NeedsAutomaticRootDependencyCalculation => true;

    protected override Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var file = processRequest.ComponentStream;

        this.Logger.LogDebug("Found Gemfile.lock {FileLocation}", file.Location);
        this.ParseGemLockFile(singleFileComponentRecorder, file);

        return Task.CompletedTask;
    }

    private void ParseGemLockFile(ISingleFileComponentRecorder singleFileComponentRecorder, IComponentStream file)
    {
        var components = new Dictionary<string, DetectedComponent>();
        var dependencies = new Dictionary<string, List<Dependency>>();

        var text = string.Empty;
        using (var reader = new StreamReader(file.Stream))
        {
            text = reader.ReadToEnd();
        }

        var lines = new List<string>(text.Split("\n"));

        while (lines.Count > 0)
        {
            if (HeadingRegex.IsMatch(lines[0].Trim()))
            {
                var heading = lines[0].Trim();
                lines.RemoveAt(0);

                // Get the lines from the section sections end with a blank line
                var sublines = new List<string>();
                while (lines.Count > 0 && lines[0].Trim().Length > 0)
                {
                    sublines.Add(lines[0]);
                    lines.RemoveAt(0);
                }

                // lines[0] is now a blank line, so lets remove it
                if (lines.Count > 0)
                {
                    lines.RemoveAt(0);
                }

                switch (heading)
                {
                    case "GIT":
                        this.ParseSection(singleFileComponentRecorder, SectionType.GIT, sublines, components, dependencies, file);
                        break;
                    case "GEM":
                        this.ParseSection(singleFileComponentRecorder, SectionType.GEM, sublines, components, dependencies, file);
                        break;
                    case "PATH":
                        this.ParseSection(singleFileComponentRecorder, SectionType.PATH, sublines, components, dependencies, file);
                        break;
                    case "BUNDLED WITH":
                        var line = sublines[0].Trim();
                        var name = "bundler";

                        // Nothing in the lockfile tells us where bundler came from
                        var addComponent = new DetectedComponent(new RubyGemsComponent(name, line, "unknown"));
                        components.TryAdd<string, DetectedComponent>(string.Format("{0}:{1}", name, file.Location), addComponent);
                        dependencies.TryAdd(string.Format("{0}:{1}", name, file.Location), []);
                        break;
                    default:
                        // We ignore other sections
                        break;
                }
            }
            else
            {
                // Throw this line away. Is this malformed? We were expecting a header
                this.Logger.LogDebug("{MalformedLine}", lines[0]);
                this.Logger.LogDebug("Appears to be malformed/is not expected here. Expected heading. {Line}", lines[0]);
                lines.RemoveAt(0);
            }
        }

        foreach (var detectedComponent in components.Values)
        {
            singleFileComponentRecorder.RegisterUsage(detectedComponent);
        }

        foreach (var key in dependencies.Keys)
        {
            foreach (var dependency in dependencies[key])
            {
                // there are cases that we ommit the dependency
                // because its version is not valid like for example
                // is a relative version instead of an absolute one
                // because of that there are children elements
                // that does not contains a entry in the dictionary
                // those elements should be removed
                if (components.ContainsKey(dependency.Id))
                {
                    singleFileComponentRecorder.RegisterUsage(components[dependency.Id], parentComponentId: components[key].Component.Id);
                }
            }
        }
    }

    private void ParseSection(ISingleFileComponentRecorder singleFileComponentRecorder, SectionType sectionType, List<string> lines, Dictionary<string, DetectedComponent> components, Dictionary<string, List<Dependency>> dependencies, IComponentStream file)
    {
        string name, remote, revision;
        name = remote = revision = string.Empty;

        var wasParentDependencyExcluded = false;

        while (lines.Count > 0)
        {
            var line = lines[0].Trim();
            lines.RemoveAt(0);
            if (line.StartsWith("remote:"))
            {
                remote = line[8..];

                // revision is only used for Git components.
                revision = string.Empty;
            }
            else if (line.StartsWith("revision:"))
            {
                revision = line[10..];
            }
            else if (line.StartsWith("specs:"))
            {
                while (lines.Count > 0)
                {
                    line = lines[0].TrimEnd();
                    lines.RemoveAt(0);
                    if (string.IsNullOrEmpty(line.Trim()))
                    {
                        break;
                    }

                    // Sub-dependency, store dependencies data of parents that were not excluded because of relative version
                    else if (SubDependencyRegex.IsMatch(line) && !wasParentDependencyExcluded)
                    {
                        var depName = line.Trim().Split(' ')[0];
                        dependencies[string.Format("{0}:{1}", name, file.Location)].Add(new Dependency(depName, file.Location));
                    }
                    else if (DependencyDefinitionRegex.IsMatch(line))
                    {
                        wasParentDependencyExcluded = false;
                        var splits = line.Trim().Split(" ");
                        name = splits[0].Trim();
                        var version = splits[1][1..^1];
                        TypedComponent newComponent;

                        if (this.IsVersionRelative(version))
                        {
                            this.Logger.LogWarning("Found component with invalid version, name = {RubyComponentName} and version = {RubyComponentVersion}", name, version);
                            singleFileComponentRecorder.RegisterPackageParseFailure($"{name} - {version}");
                            wasParentDependencyExcluded = true;
                            continue;
                        }

                        if (sectionType == SectionType.GEM || sectionType == SectionType.PATH)
                        {
                            newComponent = new RubyGemsComponent(name, version, remote);
                        }
                        else
                        {
                            newComponent = new GitComponent(new Uri(remote), revision);
                        }

                        var addComponent = new DetectedComponent(newComponent);
                        var lookupKey = string.Format("{0}:{1}", name, file.Location);

                        if (components.ContainsKey(lookupKey))
                        {
                            components.TryAdd<string, DetectedComponent>(string.Format("{0}:{1}", lookupKey, version), addComponent);
                        }
                        else
                        {
                            components.TryAdd<string, DetectedComponent>(lookupKey, addComponent);
                            dependencies.Add(lookupKey, []);
                        }
                    }
                }
            }
        }
    }

    private bool IsVersionRelative(string version)
    {
        return version.StartsWith('~') || version.StartsWith('=');
    }

    private class Dependency
    {
        public Dependency(string name, string location)
        {
            this.Name = name;
            this.Location = location;
        }

        public string Name { get; }

        public string Location { get; }

        public string Id => $"{this.Name}:{this.Location}";
    }
}
