namespace Microsoft.ComponentDetection.Detectors.Tests.DotNet;

using System.Collections.Generic;
using System.Runtime.InteropServices;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Detectors.DotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class PathRebasingUtilityTests
{
    private static readonly string RootDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:" : string.Empty;

    // A second root to simulate the build machine having a different drive or prefix.
    private static readonly string AltRootDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "D:" : "/alt";

    [TestMethod]
    public void NormalizePath_ReplacesBackslashesWithForwardSlashes()
    {
        PathRebasingUtility.NormalizePath(@"C:\path\to\file").Should().Be("C:/path/to/file");
    }

    [TestMethod]
    public void NormalizePath_ForwardSlashesUnchanged()
    {
        PathRebasingUtility.NormalizePath("C:/path/to/file").Should().Be("C:/path/to/file");
    }

    [TestMethod]
    public void NormalizePath_MixedSlashes()
    {
        PathRebasingUtility.NormalizePath(@"C:\path/to\file").Should().Be("C:/path/to/file");
    }

    [TestMethod]
    public void NormalizeDirectory_NullReturnsNull()
    {
        PathRebasingUtility.NormalizeDirectory(null).Should().BeNull();
    }

    [TestMethod]
    public void NormalizeDirectory_EmptyReturnsEmpty()
    {
        PathRebasingUtility.NormalizeDirectory(string.Empty).Should().BeEmpty();
    }

    [TestMethod]
    public void NormalizeDirectory_TrimsTrailingSeparators()
    {
        PathRebasingUtility.NormalizeDirectory(@"C:\path\to\dir\").Should().Be("C:/path/to/dir");
    }

    [TestMethod]
    public void NormalizeDirectory_TrimsMultipleTrailingSeparators()
    {
        PathRebasingUtility.NormalizeDirectory(@"C:\path\to\dir\/\/").Should().Be("C:/path/to/dir");
    }

    [TestMethod]
    public void NormalizeDirectory_NoTrailingSeparator()
    {
        PathRebasingUtility.NormalizeDirectory(@"C:\path\to\dir").Should().Be("C:/path/to/dir");
    }

    [TestMethod]
    public void GetRebaseRoot_BasicRebase_ReturnsBuildMachineRoot()
    {
        // Scan machine: C:/src/repo/path/to/project
        // Build machine: D:/a/_work/1/s/path/to/project
        var sourceDir = $"{RootDir}/src/repo";
        var sourceBasedPath = $"{RootDir}/src/repo/path/to/project";
        var artifactPath = $"{AltRootDir}/a/_work/1/s/path/to/project";

        var result = PathRebasingUtility.GetRebaseRoot(sourceDir, sourceBasedPath, artifactPath);

        result.Should().Be($"{AltRootDir}/a/_work/1/s/");
    }

    [TestMethod]
    public void GetRebaseRoot_SamePaths_ReturnsNull()
    {
        var path = $"{RootDir}/src/repo/path/to/project";

        var result = PathRebasingUtility.GetRebaseRoot($"{RootDir}/src/repo", path, path);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GetRebaseRoot_NoCommonSuffix_ReturnsNull()
    {
        // Artifact path doesn't share any relative suffix with the source-based path.
        var sourceDir = $"{RootDir}/src/repo";
        var sourceBasedPath = $"{RootDir}/src/repo/path/to/project";
        var artifactPath = $"{AltRootDir}/completely/different/layout";

        var result = PathRebasingUtility.GetRebaseRoot(sourceDir, sourceBasedPath, artifactPath);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GetRebaseRoot_NullSourceDirectory_ReturnsNull()
    {
        PathRebasingUtility.GetRebaseRoot(null, "/some/path", "/other/path").Should().BeNull();
    }

    [TestMethod]
    public void GetRebaseRoot_EmptySourceDirectory_ReturnsNull()
    {
        PathRebasingUtility.GetRebaseRoot(string.Empty, "/some/path", "/other/path").Should().BeNull();
    }

    [TestMethod]
    public void GetRebaseRoot_NullArtifactPath_ReturnsNull()
    {
        PathRebasingUtility.GetRebaseRoot("/src", "/src/path", null).Should().BeNull();
    }

    [TestMethod]
    public void GetRebaseRoot_EmptyArtifactPath_ReturnsNull()
    {
        PathRebasingUtility.GetRebaseRoot("/src", "/src/path", string.Empty).Should().BeNull();
    }

    [TestMethod]
    public void GetRebaseRoot_BackslashPaths_NormalizedBeforeComparison()
    {
        var sourceDir = $"{RootDir}/src/repo";
        var sourceBasedPath = $@"{RootDir}\src\repo\path\to\project";
        var artifactPath = $@"{AltRootDir}\a\_work\1\s\path\to\project";

        var result = PathRebasingUtility.GetRebaseRoot(sourceDir, sourceBasedPath, artifactPath);

        result.Should().Be($"{AltRootDir}/a/_work/1/s/");
    }

    [TestMethod]
    public void GetRebaseRoot_TrailingSeparatorsAreNormalized()
    {
        var sourceDir = $"{RootDir}/src/repo";
        var sourceBasedPath = $"{RootDir}/src/repo/path/to/project/";
        var artifactPath = $"{AltRootDir}/agent/path/to/project/";

        var result = PathRebasingUtility.GetRebaseRoot(sourceDir, sourceBasedPath, artifactPath);

        result.Should().Be($"{AltRootDir}/agent/");
    }

    [TestMethod]
    public void RebasePath_BasicRebase()
    {
        var originalRoot = $"{AltRootDir}/a/_work/1/s/";
        var newRoot = $"{RootDir}/src/repo";
        var path = $"{AltRootDir}/a/_work/1/s/path/to/project/obj";

        var result = PathRebasingUtility.RebasePath(path, originalRoot, newRoot);

        result.Should().Be($"{RootDir}/src/repo/path/to/project/obj");
    }

    [TestMethod]
    public void RebasePath_BackslashInput_NormalizedOutput()
    {
        var originalRoot = $@"{AltRootDir}\a\_work\1\s\";
        var newRoot = $@"{RootDir}\src\repo";
        var path = $@"{AltRootDir}\a\_work\1\s\path\to\file.csproj";

        var result = PathRebasingUtility.RebasePath(path, originalRoot, newRoot);

        result.Should().Be($"{RootDir}/src/repo/path/to/file.csproj");
    }

    [TestMethod]
    public void RebasePath_RootOnlyPath_ReturnsNewRoot()
    {
        var originalRoot = $"{AltRootDir}/build/";
        var newRoot = $"{RootDir}/scan";
        var path = $"{AltRootDir}/build";

        var result = PathRebasingUtility.RebasePath(path, originalRoot, newRoot);

        // Path equals originalRoot → relative is "." → Path.Combine preserves it.
        result.Should().Be($"{RootDir}/scan/.");
    }

    [TestMethod]
    public void RebasePath_RoundTripsWithGetRebaseRoot()
    {
        var sourceDir = $"{RootDir}/src/repo";
        var sourceBasedPath = $"{RootDir}/src/repo/subdir/project";
        var artifactPath = $"{AltRootDir}/agent/s/subdir/project";

        var rebaseRoot = PathRebasingUtility.GetRebaseRoot(sourceDir, sourceBasedPath, artifactPath);
        rebaseRoot.Should().NotBeNull();

        // Given a different path on the build machine, rebase it to the scan machine.
        var buildMachinePath = $"{AltRootDir}/agent/s/subdir/project/obj/project.assets.json";
        var result = PathRebasingUtility.RebasePath(buildMachinePath, rebaseRoot!, sourceDir);

        result.Should().Be($"{RootDir}/src/repo/subdir/project/obj/project.assets.json");
    }

    [TestMethod]
    public void RebasePath_RelativePath_ReturnedUnchanged()
    {
        var originalRoot = $"{RootDir}/build/root";
        var newRoot = $"{RootDir}/scan/root";
        var relativePath = "relative/path/file.csproj";

        var result = PathRebasingUtility.RebasePath(relativePath, originalRoot, newRoot);

        // A non-rooted path cannot be rebased — returned unchanged.
        result.Should().Be("relative/path/file.csproj");
    }

    [TestMethod]
    public void RebasePath_PathOutsideOriginalRoot_ReturnedUnchanged()
    {
        // Path is on a completely different root from originalRoot.
        var originalRoot = $"{AltRootDir}/a/_work/1/s";
        var newRoot = $"{RootDir}/src/repo";
        var outsidePath = $"{RootDir}/completely/different/file.csproj";

        var result = PathRebasingUtility.RebasePath(outsidePath, originalRoot, newRoot);

        // Cannot be rebased — returned unchanged (normalized).
        result.Should().Be($"{RootDir}/completely/different/file.csproj");
    }

    [TestMethod]
    public void FindByRelativePath_MatchesBySuffix()
    {
        var sourceDir = $"{RootDir}/src/repo";
        var scanPath = $"{RootDir}/src/repo/path/to/project/obj/project.assets.json";

        var dictionary = new Dictionary<string, string>
        {
            { $"{AltRootDir}/agent/s/path/to/project/obj/project.assets.json", "matched-value" },
        };

        var result = PathRebasingUtility.FindByRelativePath<string>(
            dictionary, sourceDir, scanPath, out var rebaseRoot);

        result.Should().Be("matched-value");
        rebaseRoot.Should().Be($"{AltRootDir}/agent/s/");
    }

    [TestMethod]
    public void FindByRelativePath_NoMatch_ReturnsDefault()
    {
        var sourceDir = $"{RootDir}/src/repo";
        var scanPath = $"{RootDir}/src/repo/path/to/project/obj/project.assets.json";

        var dictionary = new Dictionary<string, string>
        {
            { $"{AltRootDir}/completely/different/layout.json", "value" },
        };

        var result = PathRebasingUtility.FindByRelativePath<string>(
            dictionary, sourceDir, scanPath, out var rebaseRoot);

        result.Should().BeNull();
        rebaseRoot.Should().BeNull();
    }

    [TestMethod]
    public void FindByRelativePath_PathOutsideSourceDir_ReturnsDefault()
    {
        // scanMachinePath is NOT under sourceDirectory (relative path starts with "..")
        var sourceDir = $"{RootDir}/src/repo";
        var scanPath = $"{RootDir}/somewhere/else/file.json";

        var dictionary = new Dictionary<string, string>
        {
            { $"{AltRootDir}/agent/s/somewhere/else/file.json", "value" },
        };

        var result = PathRebasingUtility.FindByRelativePath<string>(
            dictionary, sourceDir, scanPath, out var rebaseRoot);

        result.Should().BeNull();
        rebaseRoot.Should().BeNull();
    }

    [TestMethod]
    public void FindByRelativePath_EmptyDictionary_ReturnsDefault()
    {
        var sourceDir = $"{RootDir}/src/repo";
        var scanPath = $"{RootDir}/src/repo/path/file.json";

        var result = PathRebasingUtility.FindByRelativePath<string>(
            [], sourceDir, scanPath, out var rebaseRoot);

        result.Should().BeNull();
        rebaseRoot.Should().BeNull();
    }

    [TestMethod]
    public void FindByRelativePath_MultipleEntries_MatchesCorrectOne()
    {
        var sourceDir = $"{RootDir}/src/repo";
        var scanPath = $"{RootDir}/src/repo/path/B/file.json";

        var dictionary = new Dictionary<string, string>
        {
            { $"{AltRootDir}/agent/path/A/file.json", "wrong" },
            { $"{AltRootDir}/agent/path/B/file.json", "correct" },
            { $"{AltRootDir}/agent/path/C/file.json", "wrong" },
        };

        var result = PathRebasingUtility.FindByRelativePath<string>(
            dictionary, sourceDir, scanPath, out var rebaseRoot);

        result.Should().Be("correct");
        rebaseRoot.Should().Be($"{AltRootDir}/agent/");
    }

    [TestMethod]
    public void GetRebaseRoot_SourceBasedPathEqualsSourceDirectory_ReturnsNull()
    {
        // When sourceDirectoryBasedPath == sourceDirectory, Path.GetRelativePath returns ".".
        // Without a common relative suffix to verify, we cannot confirm the paths correspond.
        var sourceDir = $"{RootDir}/src/repo";
        var sourceBasedPath = $"{RootDir}/src/repo";
        var artifactPath = $"{AltRootDir}/a/_work/1/s";

        var result = PathRebasingUtility.GetRebaseRoot(sourceDir, sourceBasedPath, artifactPath);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GetRebaseRoot_SourceBasedPathOutsideSourceDirectory_ReturnsNull()
    {
        // When sourceDirectoryBasedPath is NOT under sourceDirectory, GetRelativePath
        // returns a ".." relative path. The method should safely return null.
        var sourceDir = $"{RootDir}/src/repo";
        var sourceBasedPath = $"{RootDir}/somewhere/else";
        var artifactPath = $"{AltRootDir}/a/_work/1/s/somewhere/else";

        var result = PathRebasingUtility.GetRebaseRoot(sourceDir, sourceBasedPath, artifactPath);

        result.Should().BeNull();
    }
}
