namespace Microsoft.ComponentDetection.Common;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides methods for executing code asynchronously with a timeout.
/// </summary>
public static class AsyncExecution
{
    /// <summary>
    /// Executes the provided function asynchronously with a timeout.
    /// </summary>
    /// <param name="toExecute">The function to execute.</param>
    /// <param name="timeout">The timeout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <returns>The result of the function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="toExecute"/> is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the execution does not complete within the timeout.</exception>
    public static async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> toExecute, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toExecute);

        var work = Task.Run(toExecute);

        var completedInTime = await Task.Run(() => work.Wait(timeout));
        if (!completedInTime)
        {
            throw new TimeoutException($"The execution did not complete in the allotted time ({timeout.TotalSeconds} seconds) and has been terminated prior to completion");
        }

        return await work;
    }

    /// <summary>
    /// Executes the provided function asynchronously with a timeout.
    /// </summary>
    /// <param name="toExecute">The function to execute.</param>
    /// <param name="timeout">The timeout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="toExecute"/> is null.</exception>
    /// <exception cref="TimeoutException">Thrown when the execution does not complete within the timeout.</exception>
    public static async Task ExecuteVoidWithTimeoutAsync(Action toExecute, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toExecute);

        var work = Task.Run(toExecute, cancellationToken);
        var completedInTime = await Task.Run(() => work.Wait(timeout));
        if (!completedInTime)
        {
            throw new TimeoutException($"The execution did not complete in the allotted time ({timeout.TotalSeconds} seconds) and has been terminated prior to completion");
        }
    }
}
