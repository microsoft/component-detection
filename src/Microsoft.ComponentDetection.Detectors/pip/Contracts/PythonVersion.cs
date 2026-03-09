#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Pip;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

public class PythonVersion : IComparable<PythonVersion>
{
    // This is a light C# port of the python version capture regex described here:
    // https://www.python.org/dev/peps/pep-0440/#appendix-b-parsing-version-strings-with-regular-expressions
    private static readonly Regex PythonVersionRegex =
        new(@"v?(?:(?:(?<epoch>[0-9]+)!)?(?<release>[0-9]+(?:\.[0-9]+)*)(?<pre>[-_\.]?(?<pre_l>(a|b|c|rc|alpha|beta|pre|preview))[-_\.]?(?<pre_n>[0-9]+)?)?(?<post>(?:-(?<post_n1>[0-9]+))|(?:[-_\.]?(?<post_l>post|rev|r)[-_\.]?(?<post_n2>[0-9]+)?))?(?<dev>[-_\.]?(?<dev_l>dev)[-_\.]?(?<dev_n>[0-9]+)?)?)(?:\+(?<local>[a-z0-9]+(?:[-_\.][a-z0-9]+)*))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, int> PreReleaseMapping = new Dictionary<string, int> { { "a", 0 }, { "alpha", 0 }, { "b", 1 }, { "beta", 1 }, { "c", 2 }, { "rc", 2 }, { "pre", 2 }, { "preview", 2 } };

    private static readonly ConcurrentDictionary<string, PythonVersion> Cache = new();

    private readonly Match match;

    private PythonVersion(string version)
    {
        var toOperate = version;
        if (version.EndsWith(".*"))
        {
            this.Floating = true;
            toOperate = toOperate.Replace(".*", string.Empty);
        }

        this.match = PythonVersionRegex.Match(version);

        if (!this.match.Success || !string.Equals(this.match.Value, toOperate, StringComparison.OrdinalIgnoreCase))
        {
            this.Valid = false;
            return;
        }

        var groups = this.match.Groups;

        // Epoch is optional, implicitly 0 if not present
        if (groups["epoch"].Success && int.TryParse(groups["epoch"].Value, out var epoch))
        {
            this.Epoch = epoch;
        }
        else
        {
            this.Epoch = 0;
        }

        this.Release = groups["release"].Success ? groups["release"].Value : string.Empty;
        this.PreReleaseLabel = groups["pre_l"].Success ? groups["pre_l"].Value : string.Empty;

        if (groups["pre_n"].Success && int.TryParse(groups["pre_n"].Value, out var preReleaseNumber))
        {
            this.PreReleaseNumber = preReleaseNumber;
        }

        if (groups["post_n1"].Success && int.TryParse(groups["post_n1"].Value, out var postRelease1))
        {
            this.PostNumber = postRelease1;
        }

        if (groups["post_n2"].Success && int.TryParse(groups["post_n2"].Value, out var postRelease2))
        {
            this.PostNumber = postRelease2;
        }

        if (groups["dev_l"].Success)
        {
            this.DevLabel = groups["dev_l"].Value;
            this.DevNumber = 0;
        }

        if (groups["dev_n"].Success && int.TryParse(groups["dev_n"].Value, out var devNumber))
        {
            this.DevNumber = devNumber;
        }

        this.Valid = true;
    }

    public bool Valid { get; set; }

    public bool IsReleasedPackage => string.IsNullOrEmpty(this.PreReleaseLabel) && !this.PreReleaseNumber.HasValue && !this.DevNumber.HasValue;

    public int Epoch { get; set; }

    public string Release { get; set; }

    public string PreReleaseLabel { get; set; }

    public int? PreReleaseNumber { get; set; }

    public int? PostNumber { get; set; }

    public string DevLabel { get; set; }

    public int? DevNumber { get; set; }

    public bool Floating { get; set; }

    public static bool operator >(PythonVersion operand1, PythonVersion operand2) => operand1.CompareTo(operand2) == 1;

    public static bool operator <(PythonVersion operand1, PythonVersion operand2) => operand1.CompareTo(operand2) == -1;

    public static bool operator >=(PythonVersion operand1, PythonVersion operand2) => operand1.CompareTo(operand2) >= 0;

    public static bool operator <=(PythonVersion operand1, PythonVersion operand2) => operand1.CompareTo(operand2) <= 0;

    public static PythonVersion Create(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (Cache.TryGetValue(version, out var cachedVersion))
        {
            return cachedVersion;
        }

        var pythonVersion = new PythonVersion(version);

        // Multiple API call threads can be running at once, if the same version
        // is parsed twice use the last entry since they will be the same.
        Cache[version] = pythonVersion;

        return pythonVersion;
    }

    public int CompareTo(PythonVersion other)
    {
        if (other == null || !other.Valid)
        {
            return 1;
        }

        if (this.Epoch > other.Epoch)
        {
            return 1;
        }
        else if (this.Epoch < other.Epoch)
        {
            return -1;
        }

        if (!string.Equals(this.Release, other.Release, StringComparison.OrdinalIgnoreCase))
        {
            var result = CompareReleaseVersions(this, other);
            if (result != 0)
            {
                return result;
            }
        }

        var preReleaseComparison = ComparePreRelease(this, other);

        if (preReleaseComparison != 0)
        {
            return preReleaseComparison;
        }

        var postNumberComparison = ComparePostNumbers(this, other);

        if (postNumberComparison != 0)
        {
            return postNumberComparison;
        }

        var devNumberComparison = CompareDevValues(this, other);

        return devNumberComparison;
    }

    [SuppressMessage("Usage", "CA1308:Normalize String to Uppercase", Justification = "Casing cannot be overwritten.")]
    private static int ComparePreRelease(PythonVersion a, PythonVersion b)
    {
        if (string.IsNullOrEmpty(a.PreReleaseLabel) && string.IsNullOrEmpty(b.PreReleaseLabel))
        {
            return 0;
        }
        else if (string.IsNullOrEmpty(a.PreReleaseLabel))
        {
            if (a.DevNumber.HasValue)
            {
                return -1;
            }

            return 1;
        }
        else if (string.IsNullOrEmpty(b.PreReleaseLabel))
        {
            if (b.DevNumber.HasValue)
            {
                return 1;
            }

            return -1;
        }

        var aLabelWeight = PreReleaseMapping[a.PreReleaseLabel.ToLowerInvariant()];
        var bLabelWeight = PreReleaseMapping[b.PreReleaseLabel.ToLowerInvariant()];

        if (aLabelWeight > bLabelWeight)
        {
            return 1;
        }
        else if (bLabelWeight > aLabelWeight)
        {
            return -1;
        }

        var aNum = a.PreReleaseNumber ?? 0;
        var bNum = b.PreReleaseNumber ?? 0;

        if (aNum > bNum)
        {
            return 1;
        }
        else if (bNum > aNum)
        {
            return -1;
        }

        // If we get here, we need to compare the post release numbers
        return ComparePostNumbers(a, b);
    }

    private static int ComparePostNumbers(PythonVersion a, PythonVersion b)
    {
        if (!a.PostNumber.HasValue && !b.PostNumber.HasValue)
        {
            return 0;
        }

        if (a.PostNumber.HasValue && b.PostNumber.HasValue)
        {
            if (a.PostNumber.Value > b.PostNumber.Value)
            {
                return 1;
            }
            else if (b.PostNumber.Value > a.PostNumber.Value)
            {
                return -1;
            }

            // We need to compare the dev value
            return CompareDevValues(a, b);
        }
        else if (a.PostNumber.HasValue)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }

    private static int CompareDevValues(PythonVersion a, PythonVersion b)
    {
        if (!a.DevNumber.HasValue && !b.DevNumber.HasValue)
        {
            return 0;
        }

        if (a.DevNumber.HasValue && b.DevNumber.HasValue)
        {
            if (a.DevNumber.Value > b.DevNumber.Value)
            {
                return 1;
            }
            else if (b.DevNumber.Value > a.DevNumber.Value)
            {
                return -1;
            }

            return 0;
        }
        else if (a.DevNumber.HasValue)
        {
            return -1;
        }
        else
        {
            return 1;
        }
    }

    private static int CompareReleaseVersions(PythonVersion a, PythonVersion b)
    {
        var aSplit = a.Release.Split('.').Select(x => int.Parse(x)).ToList();
        var bSplit = b.Release.Split('.').Select(x => int.Parse(x)).ToList();

        int longer;
        int shorter;
        int lengthCompare;
        bool shorterFloating;

        if (aSplit.Count > bSplit.Count)
        {
            longer = aSplit.Count;
            shorter = bSplit.Count;
            shorterFloating = b.Floating;
            lengthCompare = 1;
        }
        else if (bSplit.Count > aSplit.Count)
        {
            longer = bSplit.Count;
            shorter = aSplit.Count;
            shorterFloating = a.Floating;
            lengthCompare = -1;
        }
        else
        {
            longer = bSplit.Count;
            shorter = aSplit.Count;
            shorterFloating = false;
            lengthCompare = 0;
        }

        for (var i = 0; i < shorter; i++)
        {
            if (aSplit[i] > bSplit[i])
            {
                return 1;
            }
            else if (bSplit[i] > aSplit[i])
            {
                return -1;
            }
        }

        if (longer == (shorter + 1) && shorterFloating)
        {
            return 0;
        }

        return lengthCompare;
    }
}
