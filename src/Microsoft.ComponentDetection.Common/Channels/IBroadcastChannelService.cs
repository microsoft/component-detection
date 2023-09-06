namespace Microsoft.ComponentDetection.Common.Channels;

using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

/// <summary>
/// Service for broadcasting messages to all detectors.
/// </summary>
/// <typeparam name="T">The type of message to broadcast.</typeparam>
public interface IBroadcastChannelService<T>
{
    /// <summary>
    /// Creates a broadcast channel that can be used to send messages to all detectors.
    /// </summary>
    /// <returns>A channel reader that can be used to send messages to all detectors.</returns>
    ChannelReader<T> CreateBroadcastChannel();

    /// <summary>
    /// Broadcasts a message to all detectors.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task BroadcastMessageAsync(T message, CancellationToken cancellationToken);

    /// <summary>
    /// Completes all broadcast channels, indicating that no more messages will be sent.
    /// </summary>
    void Complete();
}
