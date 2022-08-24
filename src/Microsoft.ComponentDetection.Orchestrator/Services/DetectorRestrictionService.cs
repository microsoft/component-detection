using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Exceptions;

namespace Microsoft.ComponentDetection.Orchestrator.Services
{
    [Export(typeof(IDetectorRestrictionService))]
    public class DetectorRestrictionService : IDetectorRestrictionService
    {
        [Import]
        public ILogger Logger { get; set; }

        private IList<string> oldDetectorIds = new List<string> { "MSLicenseDevNpm", "MSLicenseDevNpmList", "MSLicenseNpm", "MSLicenseNpmList" };
        private string newDetectorId = "NpmWithRoots";

        public IEnumerable<IComponentDetector> ApplyRestrictions(DetectorRestrictions argSpecifiedRestrictions, IEnumerable<IComponentDetector> detectors)
        {
            // Get a list of our default off detectors beforehand so that they can always be considered
            var defaultOffDetectors = detectors.Where(x => x is IDefaultOffComponentDetector).ToList();
            detectors = detectors.Where(x => !(x is IDefaultOffComponentDetector)).ToList();

            // If someone specifies an "allow list", use it, otherwise assume everything is allowed
            if (argSpecifiedRestrictions.AllowedDetectorIds != null && argSpecifiedRestrictions.AllowedDetectorIds.Any())
            {
                var allowedIds = argSpecifiedRestrictions.AllowedDetectorIds;

                // If we have retired detectors in the arg specified list and don't have the new detector, add the new detector
                if (allowedIds.Where(a => this.oldDetectorIds.Contains(a, StringComparer.OrdinalIgnoreCase)).Any() && !allowedIds.Contains(this.newDetectorId, StringComparer.OrdinalIgnoreCase))
                {
                    allowedIds = allowedIds.Concat(new string[] {
                        this.newDetectorId });
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
                            this.Logger.LogWarning($"The detector '{id}' has been phased out, we will run the '{this.newDetectorId}' detector which replaced its functionality.");
                        }
                    }
                }
            }

            var allCategoryName = Enum.GetName(typeof(DetectorClass), DetectorClass.All);
            var detectorCategories = argSpecifiedRestrictions.AllowedDetectorCategories;

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
                if (detectors.Count() == 0)
                {
                    throw new InvalidDetectorCategoriesException($"Categories {string.Join(",", detectorCategories)} did not match any available detectors.");
                }
            }

            if (argSpecifiedRestrictions.ExplicitlyEnabledDetectorIds != null && argSpecifiedRestrictions.ExplicitlyEnabledDetectorIds.Any())
            {
                detectors = detectors.Union(defaultOffDetectors.Where(x => argSpecifiedRestrictions.ExplicitlyEnabledDetectorIds.Contains(x.Id))).ToList();
            }

            return detectors;
        }
    }
}
