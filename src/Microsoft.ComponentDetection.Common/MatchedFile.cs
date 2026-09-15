#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System.IO;

public class MatchedFile
{
    public FileInfo File { get; set; }

    public string Pattern { get; set; }
}
