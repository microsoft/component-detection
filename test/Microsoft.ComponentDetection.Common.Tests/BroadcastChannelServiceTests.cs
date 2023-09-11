namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.Channels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class BroadcastChannelServiceTests
{
    [TestMethod]
    [Timeout(1000)]
    public async Task BroadMessage_SendsToAllConsumersAsync()
    {
        var bc = new BroadcastChannelService<int>();

        var reader1 = bc.CreateBroadcastChannel();
        var reader2 = bc.CreateBroadcastChannel();

        var results = new ConcurrentBag<int>();
        var task1 = Task.Factory.StartNew(
            async () =>
            {
                await foreach (var msg in reader1.ReadAllAsync())
                {
                    results.Add(msg);
                }
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Default);

        var task2 = Task.Factory.StartNew(
            async () =>
            {
                await foreach (var msg in reader2.ReadAllAsync())
                {
                    results.Add(msg);
                }
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Default);

        await bc.BroadcastMessageAsync(10);
        await bc.BroadcastMessageAsync(20);

        bc.Complete();

        await Task.WhenAll(task1, task2);

        results.Should().BeEquivalentTo(new[] { 10, 20, 10, 20 });
    }

    [TestMethod]
    [Timeout(60 * 1000)]
    public async Task BroadcastMessage_DoesNotDropMessagesAsync()
    {
        var bc = new BroadcastChannelService<int>();
        var results = new ConcurrentBag<int>();

        var reader = bc.CreateBroadcastChannel();
        var readerThread = CreateAndStartThread(async () =>
        {
            await foreach (var msg in reader.ReadAllAsync())
            {
                results.Add(msg);
            }
        });

        var values = Enumerable.Range(0, 2048).ToList();
        foreach (var value in values)
        {
            await bc.BroadcastMessageAsync(value);
        }

        bc.Complete();
        readerThread.Join();

        results.Should().BeEquivalentTo(values);
    }

    private static Thread CreateAndStartThread(Func<Task> func)
    {
        var thread = new Thread(async () => await func());
        thread.Start();
        return thread;
    }
}
