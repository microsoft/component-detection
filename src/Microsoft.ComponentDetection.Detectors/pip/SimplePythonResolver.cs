namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;

public class SimplePythonResolver : ISimplePythonResolver
{
    private readonly ISimplePyPiClient simplePypiClient;
    private readonly ILogger<ISimplePythonResolver> logger;

    public SimplePythonResolver(ISimplePyPiClient simplePypiClient, ILogger<ISimplePythonResolver> logger)
    {
        this.simplePypiClient = simplePypiClient;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<IList<PipGraphNode>> ResolveRootsAsync(ISingleFileComponentRecorder singleFileComponentRecorder, IList<PipDependencySpecification> initialPackages)
    {
        var state = new PythonResolverState();

        // Fill the dictionary with valid packages for the roots
        foreach (var rootPackage in initialPackages)
        {
            // If we have it, we probably just want to skip at this phase as this indicates duplicates
            if (!state.ValidVersionMap.TryGetValue(rootPackage.Name, out _))
            {
                var simplePythonProject = await this.simplePypiClient.GetSimplePypiProjectAsync(rootPackage);

                if (simplePythonProject != null && simplePythonProject.Files.Any())
                {
                    var pythonProject = this.ConvertSimplePypiProjectToSortedDictionary(simplePythonProject, rootPackage);

                    if (pythonProject.Keys.Any())
                    {
                        state.ValidVersionMap[rootPackage.Name] = pythonProject;

                        // Grab the latest version as our candidate version
                        var candidateVersion = state.ValidVersionMap[rootPackage.Name].Keys.Any()
                            ? state.ValidVersionMap[rootPackage.Name].Keys.Last() : null;

                        var node = new PipGraphNode(new PipComponent(rootPackage.Name, candidateVersion));

                        state.NodeReferences[rootPackage.Name] = node;

                        state.Roots.Add(node);

                        state.ProcessingQueue.Enqueue((rootPackage.Name, rootPackage));
                    }
                    else
                    {
                        this.logger.LogWarning(
                        "Unable to resolve package: {RootPackageName} gotten from pypi, possibly due to invalid versions. Skipping package.",
                        rootPackage.Name);
                        singleFileComponentRecorder.RegisterPackageParseFailure(rootPackage.Name);
                    }
                }
                else
                {
                    this.logger.LogWarning(
                        "Root dependency {RootPackageName} not found on pypi. Skipping package.",
                        rootPackage.Name);
                    singleFileComponentRecorder.RegisterPackageParseFailure(rootPackage.Name);
                }
            }
        }

        // Now queue packages for processing
        return await this.ProcessQueueAsync(singleFileComponentRecorder, state) ?? new List<PipGraphNode>();
    }

    private async Task<IList<PipGraphNode>> ProcessQueueAsync(ISingleFileComponentRecorder singleFileComponentRecorder, PythonResolverState state)
    {
        while (state.ProcessingQueue.Count > 0)
        {
            var (root, currentNode) = state.ProcessingQueue.Dequeue();

            // gather all dependencies for the current node
            var dependencies = (await this.FetchPackageDependenciesAsync(state, currentNode)).Where(x => !x.PackageIsUnsafe());

            foreach (var dependencyNode in dependencies)
            {
                // if we have already seen the dependency and the version we have is valid, just add the dependency to the graph
                if (state.NodeReferences.TryGetValue(dependencyNode.Name, out var node) &&
                    PythonVersionUtilities.VersionValidForSpec(node.Value.Version, dependencyNode.DependencySpecifiers))
                {
                    state.NodeReferences[currentNode.Name].Children.Add(node);
                    node.Parents.Add(state.NodeReferences[currentNode.Name]);
                }
                else if (node != null)
                {
                    this.logger.LogWarning("Candidate version ({NodeValueId}) for {DependencyName} already exists in map and the version is NOT valid.", node.Value.Id, dependencyNode.Name);
                    this.logger.LogWarning("Specifiers: {DependencySpecifiers} for package {CurrentNodeName} caused this.", string.Join(',', dependencyNode.DependencySpecifiers), currentNode.Name);

                    // The currently selected version is invalid, try to see if there is another valid version available
                    if (!await this.InvalidateAndReprocessAsync(state, node, dependencyNode))
                    {
                        this.logger.LogWarning(
                            "Version Resolution for {DependencyName} failed, assuming last valid version is used.",
                            dependencyNode.Name);

                        // there is no valid version available for the node, dependencies are incompatible,
                    }
                }
                else
                {
                    // We haven't encountered this package before, so let's fetch it and find a candidate
                    var newProject = await this.simplePypiClient.GetSimplePypiProjectAsync(dependencyNode);

                    if (newProject != null && newProject.Files.Any())
                    {
                        var result = this.ConvertSimplePypiProjectToSortedDictionary(newProject, dependencyNode);
                        if (result.Keys.Any())
                        {
                            state.ValidVersionMap[dependencyNode.Name] = result;
                            var candidateVersion = state.ValidVersionMap[dependencyNode.Name].Keys.Any()
                                ? state.ValidVersionMap[dependencyNode.Name].Keys.Last() : null;

                            this.AddGraphNode(state, state.NodeReferences[currentNode.Name], dependencyNode.Name, candidateVersion);

                            state.ProcessingQueue.Enqueue((root, dependencyNode));
                        }
                        else
                        {
                            this.logger.LogWarning(
                            "Unable to reolve dependency Package {DependencyName} gotten from Pypi possibly due to invalid versions. Skipping package",
                            dependencyNode.Name);
                            singleFileComponentRecorder.RegisterPackageParseFailure(dependencyNode.Name);
                        }
                    }
                    else
                    {
                        this.logger.LogWarning(
                            "Dependency Package {DependencyName} not found in Pypi. Skipping package",
                            dependencyNode.Name);
                        singleFileComponentRecorder.RegisterPackageParseFailure(dependencyNode.Name);
                    }
                }
            }
        }

        return state.Roots;
    }

    /// <summary>
    /// Converts a SimplePypiProject to a SortedDictionary of PythonProjectReleases.
    /// </summary>
    /// <param name="simplePypiProject"> The SimplePypiProject gotten from the api. </param>
    /// <param name="spec"> The PipDependency Specification. </param>
    /// <returns> Returns a SortedDictionary of PythonProjectReleases. </returns>
    private SortedDictionary<string, IList<PythonProjectRelease>> ConvertSimplePypiProjectToSortedDictionary(SimplePypiProject simplePypiProject, PipDependencySpecification spec)
    {
        var sortedProjectVersions = new SortedDictionary<string, IList<PythonProjectRelease>>(new PythonVersionComparer());
        foreach (var file in simplePypiProject.Files)
        {
            try
            {
                var packageType = this.GetPackageType(file.FileName);
                var version = this.GetVersionFromFileName(file.FileName);
                var parsedVersion = PythonVersion.Create(version);
                if (parsedVersion.Valid && parsedVersion.IsReleasedPackage &&
                    PythonVersionUtilities.VersionValidForSpec(version, spec.DependencySpecifiers))
                {
                    var pythonProjectRelease = new PythonProjectRelease() { PythonVersion = version, PackageType = packageType, Size = file.Size, Url = file.Url };
                    if (!sortedProjectVersions.ContainsKey(version))
                    {
                        sortedProjectVersions.Add(version, new List<PythonProjectRelease>());
                    }

                    sortedProjectVersions[version].Add(pythonProjectRelease);
                }
            }
            catch (ArgumentException ae)
            {
                this.logger.LogError(
                    ae,
                    "Release {Release} could not be added to the sorted list of pip components for spec={SpecName}. Usually this happens with unexpected PyPi version formats (e.g. prerelease/dev versions).",
                    JsonSerializer.Serialize(file),
                    spec.Name);
                continue;
            }
        }

        return sortedProjectVersions;
    }

    /// <summary>
    /// Returns the package type based on the file name.
    /// </summary>
    /// <param name="fileName"> the name of the file from simple pypi. </param>
    /// <returns>a string representing the package type.</returns>
    private string GetPackageType(string fileName)
    {
        if (fileName.EndsWith(".whl"))
        {
            return "bdist_wheel";
        }
        else if (fileName.EndsWith(".tar.gz"))
        {
            return "sdist";
        }
        else if (fileName.EndsWith(".egg"))
        {
            return "bdist_egg";
        }
        else
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Uses regex to extract the version from the file name.
    /// </summary>
    /// <param name="fileName"> the name of the file from simple pypi. </param>
    /// <returns> returns a string representing the release version. </returns>
    private string GetVersionFromFileName(string fileName)
    {
        var version = Regex.Match(fileName, @"-(\d+(\.)\w+((\+|\.)\w*)*)(.tar|-)").Groups[1];
        return version.Value;
    }

    /// <summary>
    /// Fetches the dependencies for a package.
    /// </summary>
    /// <param name="state"> The PythonResolverState. </param>
    /// <param name="spec"> The PipDependencySpecification. </param>
    /// <returns> Returns a list of PipDependencySpecification. </returns>
    private async Task<IList<PipDependencySpecification>> FetchPackageDependenciesAsync(
        PythonResolverState state,
        PipDependencySpecification spec)
    {
        var candidateVersion = state.NodeReferences[spec.Name].Value.Version;

        var packageToFetch = state.ValidVersionMap[spec.Name][candidateVersion].FirstOrDefault(x => string.Equals("bdist_wheel", x.PackageType, StringComparison.OrdinalIgnoreCase)) ??
                             state.ValidVersionMap[spec.Name][candidateVersion].FirstOrDefault(x => string.Equals("bdist_egg", x.PackageType, StringComparison.OrdinalIgnoreCase));
        if (packageToFetch == null)
        {
            return new List<PipDependencySpecification>();
        }

        var packageFileStream = await this.simplePypiClient.FetchPackageFileStreamAsync(packageToFetch.Url);

        if (packageFileStream.Length == 0)
        {
            return new List<PipDependencySpecification>();
        }

        return await this.FetchDependenciesFromPackageStreamAsync(spec.Name, candidateVersion, packageFileStream);
    }

    /// <summary>
    /// Given a package stream will unzip and return the dependencies in the metadata file.
    /// </summary>
    /// <param name="name"> The package name. </param>
    /// <param name="version"> The package version. </param>
    /// <param name="packageStream"> The package file stream. </param>
    /// <returns> Returns a list of the dependencies. </returns>
    private async Task<IList<PipDependencySpecification>> FetchDependenciesFromPackageStreamAsync(string name, string version, Stream packageStream)
    {
        var dependencies = new List<PipDependencySpecification>();
        var package = new ZipArchive(packageStream);

        var entry = package.GetEntry($"{name.Replace('-', '_')}-{version}.dist-info/METADATA");

        // If there is no metadata file, the package doesn't have any declared dependencies
        if (entry == null)
        {
            return dependencies;
        }

        var content = new List<string>();
        using (var stream = entry.Open())
        {
            using var streamReader = new StreamReader(stream);

            while (!streamReader.EndOfStream)
            {
                var line = await streamReader.ReadLineAsync();

                if (PipDependencySpecification.RequiresDistRegex.IsMatch(line))
                {
                    content.Add(line);
                }
            }
        }

        // Pull the packages that aren't conditional based on "extras"
        // Right now we just want to resolve the graph as most comsumers will
        // experience it
        foreach (var deps in content.Where(x => !x.Contains("extra ==")))
        {
            dependencies.Add(new PipDependencySpecification(deps, true));
        }

        return dependencies;
    }

    /// <summary>
    /// Given a state, node, and new spec, will reprocess a new valid version for the node.
    /// </summary>
    /// <param name="state"> The PythonResolverState. </param>
    /// <param name="node"> The PipGraphNode. </param>
    /// <param name="newSpec"> The PipDependencySpecification. </param>
    /// <returns> Returns true if the node can be reprocessed else false. </returns>
    private async Task<bool> InvalidateAndReprocessAsync(
        PythonResolverState state,
        PipGraphNode node,
        PipDependencySpecification newSpec)
    {
        var pipComponent = node.Value;

        var oldVersions = state.ValidVersionMap[pipComponent.Name].Keys.ToList();
        var currentSelectedVersion = node.Value.Version;
        var currentReleases = state.ValidVersionMap[pipComponent.Name][currentSelectedVersion];
        foreach (var version in oldVersions)
        {
            if (!PythonVersionUtilities.VersionValidForSpec(version, newSpec.DependencySpecifiers))
            {
                state.ValidVersionMap[pipComponent.Name].Remove(version);
            }
        }

        if (state.ValidVersionMap[pipComponent.Name].Count == 0)
        {
            state.ValidVersionMap[pipComponent.Name][currentSelectedVersion] = currentReleases;
            return false;
        }

        var candidateVersion = state.ValidVersionMap[pipComponent.Name].Keys.Any() ? state.ValidVersionMap[pipComponent.Name].Keys.Last() : null;

        node.Value = new PipComponent(pipComponent.Name, candidateVersion);

        var dependencies = (await this.FetchPackageDependenciesAsync(state, newSpec)).ToDictionary(x => x.Name, x => x);

        var toRemove = new List<PipGraphNode>();
        foreach (var child in node.Children)
        {
            var pipChild = child.Value;

            if (!dependencies.TryGetValue(pipChild.Name, out var newDependency))
            {
                toRemove.Add(child);
            }
            else if (!PythonVersionUtilities.VersionValidForSpec(pipChild.Version, newDependency.DependencySpecifiers))
            {
                if (!await this.InvalidateAndReprocessAsync(state, child, newDependency))
                {
                    return false;
                }
            }
        }

        foreach (var remove in toRemove)
        {
            node.Children.Remove(remove);
        }

        return true;
    }

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    /// <param name="state"> The PythonResolverState. </param>
    /// <param name="parent"> The parent node. </param>
    /// <param name="name"> The package name. </param>
    /// <param name="version"> The package version. </param>
    private void AddGraphNode(PythonResolverState state, PipGraphNode parent, string name, string version)
    {
        if (state.NodeReferences.TryGetValue(name, out var value))
        {
            parent.Children.Add(value);
            value.Parents.Add(parent);
        }
        else
        {
            var node = new PipGraphNode(new PipComponent(name, version));
            state.NodeReferences[name] = node;
            parent.Children.Add(node);
            node.Parents.Add(parent);
        }
    }
}
