#nullable disable
namespace Microsoft.ComponentDetection.Contracts;

using System.IO;

/// <summary>
/// Represents a stream that was discovered by the provided pattern to <see cref="IComponentStreamEnumerableFactory" /> />.
/// </summary>
public interface IComponentStream
{
    /// <summary>
    /// Gets the stream object that was discovered by the provided pattern to <see cref="IComponentStreamEnumerableFactory" /> />.
    /// </summary>
    Stream Stream { get; }

    /// <summary>
    /// Gets the pattern that this stream matched. Ex: If *.bar was used to match Foo.bar, this field would contain *.bar.
    /// </summary>
    string Pattern { get; }

    /// <summary>
    /// Gets the location for this stream. Often a file path if not in test circumstances.
    /// </summary>
    string Location { get; }
}
