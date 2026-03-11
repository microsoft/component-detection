namespace Microsoft.ComponentDetection.Detectors.NuGet;

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

/// <summary>
/// Represents project information extracted from an MSBuild binlog file.
/// Contains properties of interest for component classification.
/// </summary>
internal class MSBuildProjectInfo
{
    /// <summary>
    /// Maps MSBuild property names to their metadata.
    /// </summary>
    private static readonly Dictionary<string, PropertyInfo> Properties = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(IsDevelopment)] = new((info, value) => info.IsDevelopment = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)),
        [nameof(IsPackable)] = new((info, value) => info.IsPackable = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)),
        [nameof(IsShipping)] = new((info, value) => info.IsShipping = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)),
        [nameof(IsTestProject)] = new((info, value) => info.IsTestProject = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)),
        [nameof(NETCoreSdkVersion)] = new((info, value) => info.NETCoreSdkVersion = value),
        [nameof(OutputType)] = new((info, value) => info.OutputType = value),
        [nameof(PublishAot)] = new((info, value) => info.PublishAot = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)),
        [nameof(ProjectAssetsFile)] = new(
            (info, value) =>
            {
                if (!string.IsNullOrEmpty(value))
                {
                    info.ProjectAssetsFile = value;
                }
            },
            IsPath: true),
        [nameof(SelfContained)] = new((info, value) => info.SelfContained = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)),
        [nameof(TargetFramework)] = new((info, value) => info.TargetFramework = value),
        [nameof(TargetFrameworks)] = new((info, value) => info.TargetFrameworks = value),
    };

    /// <summary>
    /// Maps MSBuild item type names to their metadata.
    /// </summary>
    private static readonly Dictionary<string, ItemInfo> Items = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(PackageReference)] = new(info => info.PackageReference),
        [nameof(PackageDownload)] = new(info => info.PackageDownload),
    };

    /// <summary>
    /// Gets or sets the full path to the project file.
    /// </summary>
    public string? ProjectPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a development-only project.
    /// Corresponds to the MSBuild IsDevelopment property.
    /// </summary>
    public bool? IsDevelopment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the project is packable.
    /// Corresponds to the MSBuild IsPackable property.
    /// </summary>
    public bool? IsPackable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this project produces shipping artifacts.
    /// Corresponds to the MSBuild IsShipping property.
    /// </summary>
    public bool? IsShipping { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a test project.
    /// Corresponds to the MSBuild IsTestProject property.
    /// When true, all dependencies of this project should be classified as development dependencies.
    /// </summary>
    public bool? IsTestProject { get; set; }

    /// <summary>
    /// Gets or sets the output type of the project (e.g., "Exe", "Library", "WinExe").
    /// Corresponds to the MSBuild OutputType property.
    /// </summary>
    public string? OutputType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this project uses native AOT compilation.
    /// Corresponds to the MSBuild PublishAot property.
    /// </summary>
    public bool? PublishAot { get; set; }

    /// <summary>
    /// Gets or sets the .NET Core SDK version used to build the project.
    /// Corresponds to the MSBuild NETCoreSdkVersion property.
    /// </summary>
    public string? NETCoreSdkVersion { get; set; }

    /// <summary>
    /// Gets or sets the path to the project.assets.json file.
    /// Corresponds to the MSBuild ProjectAssetsFile property.
    /// </summary>
    public string? ProjectAssetsFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the project is self-contained.
    /// Corresponds to the MSBuild SelfContained property.
    /// </summary>
    public bool? SelfContained { get; set; }

    /// <summary>
    /// Gets or sets the target framework for single-targeted projects.
    /// Corresponds to the MSBuild TargetFramework property.
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the target frameworks for multi-targeted projects.
    /// Corresponds to the MSBuild TargetFrameworks property.
    /// </summary>
    public string? TargetFrameworks { get; set; }

    /// <summary>
    /// Gets the PackageReference items captured from the project.
    /// Keyed by package name (ItemSpec).
    /// </summary>
    public IDictionary<string, ITaskItem> PackageReference { get; } = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the PackageDownload items captured from the project.
    /// Keyed by package name (ItemSpec).
    /// </summary>
    public IDictionary<string, ITaskItem> PackageDownload { get; } = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the inner builds for multi-targeted projects.
    /// For multi-targeted projects, the outer build has TargetFrameworks set and dispatches to inner builds.
    /// Each inner build has a specific TargetFramework and its own set of properties and items.
    /// </summary>
    public IList<MSBuildProjectInfo> InnerBuilds { get; } = [];

    /// <summary>
    /// Gets a value indicating whether this is an outer build of a multi-targeted project.
    /// The outer build has TargetFrameworks set but TargetFramework is empty (it dispatches to inner builds).
    /// Inner builds have both TargetFrameworks and TargetFramework set.
    /// </summary>
    public bool IsOuterBuild => !string.IsNullOrEmpty(this.TargetFrameworks) && string.IsNullOrEmpty(this.TargetFramework);

    /// <summary>
    /// Determines whether the specified item type is one that this class captures.
    /// </summary>
    /// <param name="itemType">The MSBuild item type.</param>
    /// <param name="isPath">When true, the item's ItemSpec is a filesystem path that may need rebasing.</param>
    /// <returns>True if the item type is of interest; otherwise, false.</returns>
    public static bool IsItemTypeOfInterest(string itemType, out bool isPath)
    {
        if (Items.TryGetValue(itemType, out var info))
        {
            isPath = info.IsPath;
            return true;
        }

        isPath = false;
        return false;
    }

    /// <summary>
    /// Determines whether the specified property name is one that this class captures.
    /// </summary>
    /// <param name="propertyName">The MSBuild property name.</param>
    /// <param name="isPath">When true, the property value is a filesystem path that may need rebasing.</param>
    /// <returns>True if the property is of interest; otherwise, false.</returns>
    public static bool IsPropertyOfInterest(string propertyName, out bool isPath)
    {
        if (Properties.TryGetValue(propertyName, out var info))
        {
            isPath = info.IsPath;
            return true;
        }

        isPath = false;
        return false;
    }

    /// <summary>
    /// Sets a property value if it is one of the properties of interest.
    /// </summary>
    /// <param name="propertyName">The MSBuild property name.</param>
    /// <param name="value">The property value.</param>
    /// <returns>True if the property was set; otherwise, false.</returns>
    public bool TrySetProperty(string propertyName, string value)
    {
        if (Properties.TryGetValue(propertyName, out var info))
        {
            info.Setter(this, value);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds or updates an item if it is one of the item types of interest.
    /// </summary>
    /// <param name="itemType">The item type (e.g., "PackageReference").</param>
    /// <param name="item">The item to add or update.</param>
    /// <returns>True if the item was added or updated; otherwise, false.</returns>
    public bool TryAddOrUpdateItem(string itemType, ITaskItem item)
    {
        if (item == null || !Items.TryGetValue(itemType, out var itemInfo))
        {
            return false;
        }

        var dictionary = itemInfo.GetDictionary(this);
        dictionary[item.ItemSpec] = item;
        return true;
    }

    /// <summary>
    /// Removes an item if it exists.
    /// </summary>
    /// <param name="itemType">The item type (e.g., "PackageReference").</param>
    /// <param name="itemSpec">The item spec (e.g., package name).</param>
    /// <returns>True if the item was removed; otherwise, false.</returns>
    public bool TryRemoveItem(string itemType, string itemSpec)
    {
        if (!Items.TryGetValue(itemType, out var info))
        {
            return false;
        }

        var dictionary = info.GetDictionary(this);
        return dictionary.Remove(itemSpec);
    }

    /// <summary>
    /// Merges another project info into this one, forming a superset.
    /// This is used when the same project is seen multiple times (e.g., build + publish passes).
    /// In practice, property values are not expected to differ across passes for the same project
    /// and target framework — the merge fills in any values that were not set rather than overriding.
    /// Boolean properties use logical OR (any true value is sufficient to classify the project).
    /// Items from <paramref name="other"/> are added if not already present.
    /// </summary>
    /// <param name="other">The other project info to merge from.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public MSBuildProjectInfo MergeWith(MSBuildProjectInfo other)
    {
        // Merge boolean properties: true wins. For all classification booleans (IsTestProject,
        // IsDevelopment, IsShipping, etc.), if any pass reports true it is sufficient to classify
        // the project accordingly. These values are not expected to differ across passes.
        this.IsDevelopment = MergeBool(this.IsDevelopment, other.IsDevelopment);
        this.IsPackable = MergeBool(this.IsPackable, other.IsPackable);
        this.IsShipping = MergeBool(this.IsShipping, other.IsShipping);
        this.IsTestProject = MergeBool(this.IsTestProject, other.IsTestProject);
        this.PublishAot = MergeBool(this.PublishAot, other.PublishAot);
        this.SelfContained = MergeBool(this.SelfContained, other.SelfContained);

        // Merge string properties: fill in unset values only.
        // These are not expected to differ across passes for the same project/TFM.
        this.OutputType ??= other.OutputType;
        this.NETCoreSdkVersion ??= other.NETCoreSdkVersion;
        this.ProjectAssetsFile ??= other.ProjectAssetsFile;
        this.TargetFramework ??= other.TargetFramework;
        this.TargetFrameworks ??= other.TargetFrameworks;

        // Merge items: add items from other that are not already present
        MergeItems(this.PackageReference, other.PackageReference);
        MergeItems(this.PackageDownload, other.PackageDownload);

        return this;
    }

    private static bool? MergeBool(bool? existing, bool? incoming)
    {
        if (existing == true || incoming == true)
        {
            return true;
        }

        return existing ?? incoming;
    }

    private static void MergeItems(IDictionary<string, ITaskItem> target, IDictionary<string, ITaskItem> source)
    {
        foreach (var kvp in source)
        {
            // TryAdd: only add if not already present (existing items win)
            target.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Metadata for a tracked MSBuild property: its setter and whether its value is a filesystem path.
    /// </summary>
    private record PropertyInfo(Action<MSBuildProjectInfo, string> Setter, bool IsPath = false);

    /// <summary>
    /// Metadata for a tracked MSBuild item type: its dictionary accessor and whether its ItemSpec is a filesystem path.
    /// </summary>
    private record ItemInfo(Func<MSBuildProjectInfo, IDictionary<string, ITaskItem>> GetDictionary, bool IsPath = false);
}
