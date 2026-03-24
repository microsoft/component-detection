#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn.Parsers;

using System.Collections.Generic;

public class YarnBlock
{
    /// <summary>
    /// Gets or sets the first line of the block, without the semicolon.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets the key/value pairs that the block contains.
    /// </summary>
    public IDictionary<string, string> Values { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets child blocks, as dentoed by "{child}:".
    /// </summary>
    public IList<YarnBlock> Children { get; } = [];
}
