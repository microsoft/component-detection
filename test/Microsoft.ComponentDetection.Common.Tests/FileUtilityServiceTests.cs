namespace Microsoft.ComponentDetection.Common.Tests;

using System.IO;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class FileUtilityServiceTests
{
    private readonly IFileUtilityService fileUtilityService;

    public FileUtilityServiceTests() =>
        this.fileUtilityService = new FileUtilityService();

    [TestMethod]
    public void DuplicateFileWithoutLines_WithLinesToRemove_ShouldCreateDuplicateFileWithoutLines()
    {
        // Get directory of current executing assembly
        var directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // Arrange
        var fileName = $"{directory}/Resources/test-file-duplicate.txt";
        var removalIndicator = "//REMOVE";
        var expectedDuplicateFilePath = Path.Combine(directory, "Resources", "temp.test-file-duplicate.txt");

        // Act
        var (duplicateFilePath, createdDuplicate) = this.fileUtilityService.DuplicateFileWithoutLines(fileName, removalIndicator);

        // Assert
        createdDuplicate.Should().BeTrue();
        duplicateFilePath.Should().Be(expectedDuplicateFilePath);
        File.Exists(expectedDuplicateFilePath).Should().BeTrue();
    }

    [TestMethod]
    public void DuplicateFileWithoutLines_WithoutLinesToRemove_ShouldNotCreateDuplicateFile()
    {
        // Get directory of current executing assembly
        var directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // Arrange
        var fileName = $"{directory}/Resources/test-file-duplicate.txt";
        var removalIndicator = "//NOTEXIST";

        // Act
        var (duplicateFilePath, createdDuplicate) = this.fileUtilityService.DuplicateFileWithoutLines(fileName, removalIndicator);

        // Assert
        createdDuplicate.Should().BeFalse();
        duplicateFilePath.Should().BeNull();
    }
}
