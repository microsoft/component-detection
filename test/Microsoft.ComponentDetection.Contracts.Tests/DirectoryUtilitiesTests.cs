namespace Microsoft.ComponentDetection.Contracts.Tests;

using System.IO;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DirectoryUtilitiesTests
{
    [TestMethod]
    public void GetFilesAndDirectories_WithLargeDepth_ShouldReturnFilesAndDirectories()
    {
        // Get directory of current executing assembly
        var directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // Arrange
        var patterns = new[] { "*.pyc", "__pycache__" };
        var depth = 10;

        // Act
        var (files, directories) = DirectoryUtilities.GetFilesAndDirectories(directory, patterns, depth);

        // Assert
        files.Should().NotBeEmpty();
        directories.Should().NotBeEmpty();

        files.Should().Contain($"{directory}\\Resources\\__pycache__\\testing.pyc");
        directories.Should().Contain($"{directory}\\Resources\\__pycache__");
    }

    [TestMethod]
    public void GetFilesAndDirectories_WithOneDepth_ShouldReturnDirectory()
    {
        // Get directory of current executing assembly
        var directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // Arrange
        var patterns = new[] { "*.pyc", "__pycache__" };
        var depth = 1;

        // Act
        var (files, directories) = DirectoryUtilities.GetFilesAndDirectories(directory, patterns, depth);

        // Assert does not find file, as the file is one level below the __pycache__ directory
        files.Should().BeEmpty();
        directories.Should().NotBeEmpty();
        directories.Should().Contain($"{directory}\\Resources\\__pycache__");
    }

    [TestMethod]
    public void GetFilesAndDirectories_WithNoDepth_ShouldReturnNothing()
    {
        // Get directory of current executing assembly
        var directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // Arrange
        var patterns = new[] { "*.pyc", "__pycache__" };
        var depth = 0;

        // Act
        var (files, directories) = DirectoryUtilities.GetFilesAndDirectories(directory, patterns, depth);

        // Assert
        files.Should().BeEmpty();
        directories.Should().BeEmpty();
    }
}
