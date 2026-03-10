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
    /// Maps MSBuild property names to their setter actions.
    /// </summary>
    private static readonly Dictionary<string, Action<MSBuildProjectInfo, string>> PropertySetters = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(IsDevelopment)] = (info, value) => info.IsDevelopment = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
        [nameof(IsPackable)] = (info, value) => info.IsPackable = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
        [nameof(IsShipping)] = (info, value) => info.IsShipping = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
        [nameof(IsTestProject)] = (info, value) => info.IsTestProject = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
        [nameof(NETCoreSdkVersion)] = (info, value) => info.NETCoreSdkVersion = value,
        [nameof(OutputType)] = (info, value) => info.OutputType = value,
        [nameof(PublishAot)] = (info, value) => info.PublishAot = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
        [nameof(ProjectAssetsFile)] = (info, value) =>
        {
            if (!string.IsNullOrEmpty(value))
            {
                info.ProjectAssetsFile = value;
            }
        },
        [nameof(SelfContained)] = (info, value) => info.SelfContained = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
        [nameof(TargetFramework)] = (info, value) => info.TargetFramework = value,
        [nameof(TargetFrameworks)] = (info, value) => info.TargetFrameworks = value,
    };

    /// <summary>
    /// Maps MSBuild item type names to their dictionary accessor.
    /// </summary>
    private static readonly Dictionary<string, Func<MSBuildProjectInfo, IDictionary<string, ITaskItem>>> ItemDictionaries = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(PackageReference)] = info => info.PackageReference,
        [nameof(PackageDownload)] = info => info.PackageDownload,
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
    /// <returns>True if the item type is of interest; otherwise, false.</returns>
    public static bool IsItemTypeOfInterest(string itemType) => ItemDictionaries.ContainsKey(itemType);

    /// <summary>
    /// Determines whether the specified property name is one that this class captures.
    /// </summary>
    /// <param name="propertyName">The MSBuild property name.</param>
    /// <returns>True if the property is of interest; otherwise, false.</returns>
    public static bool IsPropertyOfInterest(string propertyName) => PropertySetters.ContainsKey(propertyName);

    /// <summary>
    /// Sets a property value if it is one of the properties of interest.
    /// </summary>
    /// <param name="propertyName">The MSBuild property name.</param>
    /// <param name="value">The property value.</param>
    /// <returns>True if the property was set; otherwise, false.</returns>
    public bool TrySetProperty(string propertyName, string value)
    {
        if (PropertySetters.TryGetValue(propertyName, out var setter))
        {
            setter(this, value);
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
        if (item == null || !ItemDictionaries.TryGetValue(itemType, out var getDictionary))
        {
            return false;
        }

        var dictionary = getDictionary(this);
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
        if (!ItemDictionaries.TryGetValue(itemType, out var getDictionary))
        {
            return false;
        }

        var dictionary = getDictionary(this);
        return dictionary.Remove(itemSpec);
    }

    /// <summary>
    /// Merges another project info into this one, forming a superset.
    /// Properties from <paramref name="other"/> override null values in this instance.
    /// Boolean properties use logical OR (true wins).
    /// Items from <paramref name="other"/> are added if not already present.
    /// </summary>
    /// <param name="other">The other project info to merge from.</param>
    public void MergeWith(MSBuildProjectInfo other)
    {
        // Merge boolean properties: true wins (if either says true, result is true)
        this.IsDevelopment = MergeBool(this.IsDevelopment, other.IsDevelopment);
        this.IsPackable = MergeBool(this.IsPackable, other.IsPackable);
        this.IsShipping = MergeBool(this.IsShipping, other.IsShipping);
        this.IsTestProject = MergeBool(this.IsTestProject, other.IsTestProject);
        this.PublishAot = MergeBool(this.PublishAot, other.PublishAot);
        this.SelfContained = MergeBool(this.SelfContained, other.SelfContained);

        // Merge string properties: prefer non-null/non-empty
        this.OutputType ??= other.OutputType;
        this.NETCoreSdkVersion ??= other.NETCoreSdkVersion;
        this.ProjectAssetsFile ??= other.ProjectAssetsFile;
        this.TargetFramework ??= other.TargetFramework;
        this.TargetFrameworks ??= other.TargetFrameworks;

        // Merge items: add items from other that are not already present
        MergeItems(this.PackageReference, other.PackageReference);
        MergeItems(this.PackageDownload, other.PackageDownload);
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
}
