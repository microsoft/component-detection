namespace Microsoft.ComponentDetection.Common.Channels;

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

/// <inheritdoc />
public class BroadcastChannelService<T> : IBroadcastChannelService<T>
{
    /// <summary>
    /// The capacity of the broadcast channel. Writes will be blocked until there is space in the channel.
    /// </summary>
    public const int Capacity = 1024;

    private readonly ConcurrentBag<ChannelWriter<T>> writers = new();

    /// <inheritdoc />
    public ChannelReader<T> CreateBroadcastChannel()
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
        });

        this.writers.Add(channel.Writer);
        return channel.Reader;
    }

    /// <inheritdoc />
    public async Task BroadcastMessageAsync(T message, CancellationToken cancellationToken = default)
    {
        foreach (var writer in this.writers)
        {
            await writer.WriteAsync(message, cancellationToken);
        }
    }

    /// <inheritdoc />
    public void Complete()
    {
        foreach (var writer in this.writers)
        {
            writer.Complete();
        }
    }
}
