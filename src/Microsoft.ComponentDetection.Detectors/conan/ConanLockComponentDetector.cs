﻿namespace Microsoft.ComponentDetection.Detectors.Conan;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Conan.Contracts;
using Microsoft.Extensions.Logging;

public class ConanLockComponentDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    public ConanLockComponentDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<ConanLockComponentDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    public override string Id => "ConanLock";

    public override IList<string> SearchPatterns => new List<string> { "conan.lock" };

    public override IEnumerable<ComponentType> SupportedComponentTypes => new[] { ComponentType.Conan };

    public override int Version { get; } = 1;

    public override IEnumerable<string> Categories => new List<string> { "Conan" };

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs)
    {
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        var conanLockFile = processRequest.ComponentStream;

        try
        {
            var conanLock = await JsonSerializer.DeserializeAsync<ConanLock>(conanLockFile.Stream);
            this.RecordLockfileVersion(conanLock.Version);
            if (conanLock.Version != "0.4")
            {
                this.Logger.LogWarning("Unsupported conan.lock file version '{ConanLockVersion}'. Failed to process conan.lock file '{ConanLockLocation}'", conanLock.Version, conanLockFile.Location);
                return;
            }

            if (!conanLock.HasNodes())
            {
                return;
            }

            var packagesDictionary = conanLock.GraphLock.Nodes;
            var explicitReferencedDependencies = new HashSet<string>();
            var developmentDependencies = new HashSet<string>();
            if (packagesDictionary.ContainsKey("0"))
            {
                packagesDictionary.Remove("0", out var rootNode);
                if (rootNode.Requires != null)
                {
                    explicitReferencedDependencies = new HashSet<string>(rootNode.Requires);
                }

                if (rootNode.BuildRequires != null)
                {
                    developmentDependencies = new HashSet<string>(rootNode.BuildRequires);
                }
            }

            foreach (var (packageIndex, package) in packagesDictionary)
            {
                singleFileComponentRecorder.RegisterUsage(
                    new DetectedComponent(package.ToComponent()),
                    isExplicitReferencedDependency: explicitReferencedDependencies.Contains(packageIndex),
                    isDevelopmentDependency: developmentDependencies.Contains(packageIndex));
            }

            foreach (var (conanPackageIndex, package) in packagesDictionary)
            {
                var parentPackages = packagesDictionary.Values.Where(package => package.Requires?.Contains(conanPackageIndex) == true);
                foreach (var parentPackage in parentPackages)
                {
                    singleFileComponentRecorder.RegisterUsage(new DetectedComponent(package.ToComponent()), false, parentPackage.ToComponent().Id, isDevelopmentDependency: false);
                }
            }
        }
        catch (Exception e)
        {
            // If something went wrong, just ignore the file
            this.Logger.LogError(e, "Failed to process conan.lock file '{ConanLockLocation}'", conanLockFile.Location);
        }
    }
}
