namespace Microsoft.ComponentDetection.Orchestrator.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Exceptions;
using Microsoft.Extensions.Logging;

public class DetectorRestrictionService : IDetectorRestrictionService
{
    private readonly IList<string> oldDetectorIds = ["MSLicenseDevNpm", "MSLicenseDevNpmList", "MSLicenseNpm", "MSLicenseNpmList"];
    private readonly string newDetectorId = "NpmWithRoots";

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
            var allowedIds = restrictions.AllowedDetectorIds;

            // If we have retired detectors in the arg specified list and don't have the new detector, add the new detector
            if (allowedIds.Any(a => this.oldDetectorIds.Contains(a, StringComparer.OrdinalIgnoreCase)) && !allowedIds.Contains(this.newDetectorId, StringComparer.OrdinalIgnoreCase))
            {
                allowedIds = allowedIds.Concat([
                    this.newDetectorId,
                ]);
            }

            detectors = detectors.Where(d => allowedIds.Contains(d.Id, StringComparer.OrdinalIgnoreCase)).ToList();

            foreach (var id in allowedIds)
            {
                if (!detectors.Select(d => d.Id).Contains(id, StringComparer.OrdinalIgnoreCase))
                {
                    if (!this.oldDetectorIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new InvalidDetectorFilterException($"Detector '{id}' was not found");
                    }
                    else
                    {
                        this.logger.LogWarning("The detector '{OldId}' has been phased out, we will run the '{NewId}' detector which replaced its functionality.", id, this.newDetectorId);
                    }
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
