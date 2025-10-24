#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;

/// <inheritdoc />
public class FileUtilityService : IFileUtilityService
{
    /// <inheritdoc />
    public string ReadAllText(string filePath)
    {
        return File.ReadAllText(filePath);
    }

    /// <inheritdoc />
    public string ReadAllText(FileInfo file)
    {
        return File.ReadAllText(file.FullName);
    }

    /// <inheritdoc />
    public async Task<string> ReadAllTextAsync(FileInfo file)
    {
        return await File.ReadAllTextAsync(file.FullName);
    }

    /// <inheritdoc />
    public bool Exists(string fileName)
    {
        return File.Exists(fileName);
    }

    /// <inheritdoc />
    public Stream MakeFileStream(string fileName)
    {
        return new FileStream(fileName, FileMode.Open, FileAccess.Read);
    }

    /// <inheritdoc />
    public void Delete(string path) => File.Delete(path);

    /// <inheritdoc />
    public (string DuplicateFilePath, bool CreatedDuplicate) DuplicateFileWithoutLines(string fileName, params string[] removalIndicators)
    {
        // Read all lines from the file and filter out the lines that start with the removal indicator.
        var removedAnyLines = false;
        var linesToKeep = new List<string>();
        foreach (var line in File.ReadLines(fileName))
        {
            if (string.IsNullOrEmpty(line) || removalIndicators.Any(removalIndicator => line.Trim().StartsWith(removalIndicator, System.StringComparison.OrdinalIgnoreCase)))
            {
                removedAnyLines = true;
            }
            else
            {
                linesToKeep.Add(line);
            }
        }

        // If the file did not have any lines to remove, return null.
        if (!removedAnyLines)
        {
            return (null, false);
        }

        // Otherwise, write the lines to a new file and return the new file path.
        var duplicateFileName = $"temp.{Path.GetFileName(fileName)}";
        var duplicateFilePath = Path.Combine(Path.GetDirectoryName(fileName), duplicateFileName);
        File.WriteAllLines(duplicateFilePath, linesToKeep);
        return (duplicateFilePath, true);
    }
}
