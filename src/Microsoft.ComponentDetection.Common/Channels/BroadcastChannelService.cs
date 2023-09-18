namespace Microsoft.ComponentDetection.Common.Channels;

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

/// <inheritdoc />
public class BroadcastChannelService<T> : IBroadcastChannelService<T>
{
    private readonly ConcurrentBag<ChannelWriter<T>> writers = new();

    /// <inheritdoc />
    public ChannelReader<T> CreateBroadcastChannel()
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1024));

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
