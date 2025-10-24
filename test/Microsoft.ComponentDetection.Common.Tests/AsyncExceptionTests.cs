#nullable disable
namespace Microsoft.ComponentDetection.Common.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class AsyncExceptionTests
{
    [TestMethod]
    public async Task ExecuteWithTimeoutAsync_ThrowsNullExceptionAsync()
    {
        Func<Task<int>> toExecute = null;

        var func = async () => await AsyncExecution.ExecuteWithTimeoutAsync(toExecute, TimeSpan.FromSeconds(1), CancellationToken.None);

        await func.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task ExecuteWithoutTimeAsync_ThrowsTimeoutExceptionAsync()
    {
        static async Task<int> ToExecuteAsync()
        {
            await Task.Delay(5000);
            return 0;
        }

        var func = async () => await AsyncExecution.ExecuteWithTimeoutAsync(ToExecuteAsync, TimeSpan.FromSeconds(1), CancellationToken.None);

        await func.Should().ThrowAsync<TimeoutException>();
    }

    [TestMethod]
    public async Task ExecuteVoidWithTimeoutAsync_ThrowsNullExceptionAsync()
    {
        Action toExecute = null;

        var func = async () => await AsyncExecution.ExecuteVoidWithTimeoutAsync(toExecute, TimeSpan.FromSeconds(1), CancellationToken.None);

        await func.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task ExecuteVoidWithTimeoutAsync_ThrowsTimeoutExceptionAsync()
    {
        static void ToExecute() => Task.Delay(5000).Wait();

        var func = async () => await AsyncExecution.ExecuteVoidWithTimeoutAsync(ToExecute, TimeSpan.FromSeconds(1), CancellationToken.None);

        await func.Should().ThrowAsync<TimeoutException>();
    }
}
