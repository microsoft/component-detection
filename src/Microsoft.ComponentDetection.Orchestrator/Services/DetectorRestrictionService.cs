namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Exceptions;
using Microsoft.Extensions.Logging;

internal class DetectorRestrictionService : IDetectorRestrictionService
{
    private static readonly IReadOnlyDictionary<string, string> DetectorReplacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["MSLicenseDevNpm"] = "NpmWithRoots",
        ["MSLicenseDevNpmList"] = "NpmWithRoots",
        ["MSLicenseNpm"] = "NpmWithRoots",
        ["MSLicenseNpmList"] = "NpmWithRoots",
        ["DotNet"] = "MSBuildBinaryLog",
        ["NuGetProjectCentric"] = "MSBuildBinaryLog",
    };

    private readonly ILogger<DetectorRestrictionService> logger;

    public DetectorRestrictionService(ILogger<DetectorRestrictionService> logger) => this.logger = logger;

    public IEnumerable<IComponentDetector> ApplyRestrictions(DetectorRestrictions restrictions, IEnumerable<IComponentDetector> detectors)
    {
        // Get a list of our default off detectors beforehand so that they can always be considered
        var defaultOffDetectors = detectors.Where(x => x is IDefaultOffComponentDetector).ToList();
        detectors = detectors.Where(x => !(x is IDefaultOffComponentDetector)).ToList();

        // If someone specifies an "allow list", use it, otherwise assume everything is allowed
        if (restrictions.AllowedDetectorIds != null && restrictions.AllowedDetectorIds.Any())
        {
            var allowedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var requestedId in restrictions.AllowedDetectorIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (DetectorReplacements.TryGetValue(requestedId, out var replacementId))
                {
                    allowedIds.Add(replacementId);
                    this.logger.LogWarning("The detector '{OldId}' has been phased out, we will run the '{NewId}' detector which replaced its functionality.", requestedId, replacementId);
                }
                else
                {
                    allowedIds.Add(requestedId);
                }
            }

            detectors = detectors.Where(d => allowedIds.Contains(d.Id, StringComparer.OrdinalIgnoreCase)).ToList();

            foreach (var id in allowedIds)
            {
                if (!detectors.Select(d => d.Id).Contains(id, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidDetectorFilterException($"Detector '{id}' was not found");
                }
            }
        }

        var allCategoryName = Enum.GetName(typeof(DetectorClass), DetectorClass.All);
        var detectorCategories = restrictions.AllowedDetectorCategories;

        // If someone specifies an "allow list", use it, otherwise assume everything is allowed
        if (detectorCategories != null && detectorCategories.Any() && !detectorCategories.Contains(allCategoryName))
        {
            detectors = detectors.Where(x =>
            {
                if (x.Categories != null)
                {
                    // If a detector specifies the "All" category or its categories intersect with the requested categories.
                    return x.Categories.Contains(allCategoryName) || detectorCategories.Intersect(x.Categories).Any();
                }

                return false;
            }).ToList();
            if (!detectors.Any())
            {
                throw new InvalidDetectorCategoriesException($"Categories {string.Join(",", detectorCategories)} did not match any available detectors.");
            }
        }

        if (restrictions.ExplicitlyEnabledDetectorIds != null && restrictions.ExplicitlyEnabledDetectorIds.Any())
        {
            detectors = detectors.Union(defaultOffDetectors.Where(x => restrictions.ExplicitlyEnabledDetectorIds.Contains(x.Id))).ToList();
        }

        return detectors;
    }
}
