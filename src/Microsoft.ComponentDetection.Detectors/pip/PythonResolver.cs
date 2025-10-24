#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Newtonsoft.Json;

public class PythonResolver : PythonResolverBase, IPythonResolver
{
    private readonly IPyPiClient pypiClient;
    private readonly ILogger<PythonResolver> logger;
    private readonly Dictionary<string, string> pythonEnvironmentVariables = [];

    private readonly int maxLicenseFieldLength = 100;
    private readonly string classifierFieldSeparator = " :: ";
    private readonly string classifierFieldLicensePrefix = "License";

    public PythonResolver(IPyPiClient pypiClient, ILogger<PythonResolver> logger)
        : base(logger)
    {
        this.pypiClient = pypiClient;
        this.logger = logger;
    }

    /// <summary>
    /// Resolves the root Python packages from the initial list of packages.
    /// </summary>
    /// <param name="singleFileComponentRecorder">The component recorder for file that is been processed.</param>
    /// <param name="initialPackages">The initial list of packages.</param>
    /// <returns>The root packages, with dependencies associated as children.</returns>
    public async Task<IList<PipGraphNode>> ResolveRootsAsync(ISingleFileComponentRecorder singleFileComponentRecorder, IList<PipDependencySpecification> initialPackages)
    {
        var state = new PythonResolverState();

        // Fill the dictionary with valid packages for the roots
        foreach (var rootPackage in initialPackages)
        {
            // If we have it, we probably just want to skip at this phase as this indicates duplicates
            if (!state.ValidVersionMap.TryGetValue(rootPackage.Name, out _))
            {
                var project = await this.pypiClient.GetProjectAsync(rootPackage);

                var result = project.Releases;

                if (result is not null && result.Keys.Count != 0)
                {
                    state.ValidVersionMap[rootPackage.Name] = result;

                    // Grab the latest version as our candidate version
                    var candidateVersion = state.ValidVersionMap[rootPackage.Name].Keys.Count != 0
                        ? state.ValidVersionMap[rootPackage.Name].Keys.Last() : null;

                    var node = new PipGraphNode(new PipComponent(rootPackage.Name, candidateVersion, license: this.GetLicenseFromProject(project), author: this.GetSupplierFromProject(project)));

                    state.NodeReferences[rootPackage.Name] = node;

                    state.Roots.Add(node);

                    state.ProcessingQueue.Enqueue((rootPackage.Name, rootPackage));
                }
                else
                {
                    this.logger.LogWarning(
                        "Unable to resolve root dependency {PackageName} with version specifiers {PackageVersions} from pypi possibly due to computed version constraints. Skipping package.",
                        rootPackage.Name,
                        JsonConvert.SerializeObject(rootPackage.DependencySpecifiers));
                    singleFileComponentRecorder.RegisterPackageParseFailure(rootPackage.Name);
                }
            }
        }

        // Now queue packages for processing
        return await this.ProcessQueueAsync(singleFileComponentRecorder, state) ?? [];
    }

    private async Task<IList<PipGraphNode>> ProcessQueueAsync(ISingleFileComponentRecorder singleFileComponentRecorder, PythonResolverState state)
    {
        while (state.ProcessingQueue.Count > 0)
        {
            var (root, currentNode) = state.ProcessingQueue.Dequeue();

            // gather all dependencies for the current node
            var dependencies = (await this.FetchPackageDependenciesAsync(state, currentNode)).Where(x => x.IsValidParentPackage(this.pythonEnvironmentVariables)).ToList();

            foreach (var dependencyNode in dependencies)
            {
                try
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
                        var project = await this.pypiClient.GetProjectAsync(dependencyNode);

                        var result = project.Releases;

                        if (result is not null && result.Keys.Count != 0)
                        {
                            state.ValidVersionMap[dependencyNode.Name] = result;
                            var candidateVersion = state.ValidVersionMap[dependencyNode.Name].Keys.Count != 0
                                ? state.ValidVersionMap[dependencyNode.Name].Keys.Last() : null;

                            this.AddGraphNode(state, state.NodeReferences[currentNode.Name], dependencyNode.Name, candidateVersion, license: this.GetLicenseFromProject(project), author: this.GetSupplierFromProject(project));

                            state.ProcessingQueue.Enqueue((root, dependencyNode));
                        }
                        else
                        {
                            this.logger.LogWarning(
                                "Unable to resolve non-root dependency {PackageName} with version specifiers {PackageVersions} from pypi possibly due to computed version constraints. Skipping package.",
                                dependencyNode.Name,
                                JsonConvert.SerializeObject(dependencyNode.DependencySpecifiers));
                            singleFileComponentRecorder.RegisterPackageParseFailure(dependencyNode.Name);
                        }
                    }
                }
                catch (ArgumentException ae)
                {
                    // If version specifier parsing fails, don't attempt to reprocess because it would fail also.
                    // Log a package failure warning and continue.
                    this.logger.LogWarning("Failure resolving Python package {DependencyName} with message: {ExMessage}.", dependencyNode.Name, ae.Message);
                    singleFileComponentRecorder.RegisterPackageParseFailure(dependencyNode.Name);
                }
            }
        }

        return state.Roots;
    }

    protected override async Task<IList<PipDependencySpecification>> FetchPackageDependenciesAsync(
        PythonResolverState state,
        PipDependencySpecification spec)
    {
        var candidateVersion = state.NodeReferences[spec.Name].Value.Version;

        var packageToFetch = state.ValidVersionMap[spec.Name][candidateVersion].FirstOrDefault(x => string.Equals("bdist_wheel", x.PackageType, StringComparison.OrdinalIgnoreCase)) ??
                             state.ValidVersionMap[spec.Name][candidateVersion].FirstOrDefault(x => string.Equals("bdist_egg", x.PackageType, StringComparison.OrdinalIgnoreCase));
        if (packageToFetch == null)
        {
            return [];
        }

        return await this.pypiClient.FetchPackageDependenciesAsync(spec.Name, candidateVersion, packageToFetch);
    }

    private void AddGraphNode(PythonResolverState state, PipGraphNode parent, string name, string version, string license = null, string author = null)
    {
        if (state.NodeReferences.TryGetValue(name, out var value))
        {
            parent.Children.Add(value);
            value.Parents.Add(parent);
        }
        else
        {
            var node = new PipGraphNode(new PipComponent(name, version, license: license, author: author));
            state.NodeReferences[name] = node;
            parent.Children.Add(node);
            node.Parents.Add(parent);
        }
    }

    private string GetSupplierFromProject(PythonProject project)
    {
        if (!string.IsNullOrWhiteSpace(project.Info?.Maintainer))
        {
            return project.Info.Maintainer;
        }

        if (!string.IsNullOrWhiteSpace(project.Info?.MaintainerEmail))
        {
            return project.Info.MaintainerEmail;
        }

        if (!string.IsNullOrWhiteSpace(project.Info?.Author))
        {
            return project.Info.Author;
        }

        if (!string.IsNullOrWhiteSpace(project.Info?.AuthorEmail))
        {
            return project.Info.AuthorEmail;
        }

        // If none of the fields are populated, return null.
        return null;
    }

    private string GetLicenseFromProject(PythonProject project)
    {
        // There are cases where the actual license text is found in the license field so we limit the length of this field to 100 characters.
        if (project.Info?.License != null && project.Info?.License.Length < this.maxLicenseFieldLength)
        {
            return project.Info.License;
        }

        if (project.Info?.Classifiers != null)
        {
            var licenseClassifiers = project.Info.Classifiers.Where(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith(this.classifierFieldLicensePrefix));

            // Split the license classifiers by the " :: " and take the last part of the string
            licenseClassifiers = licenseClassifiers.Select(x => x.Split(this.classifierFieldSeparator).Last()).ToList();

            return string.Join(", ", licenseClassifiers);
        }

        return null;
    }

    public void SetPythonEnvironmentVariable(string key, string value)
    {
        this.pythonEnvironmentVariables[key] = value;
    }

    public Dictionary<string, string> GetPythonEnvironmentVariables()
    {
        return this.pythonEnvironmentVariables;
    }
}
