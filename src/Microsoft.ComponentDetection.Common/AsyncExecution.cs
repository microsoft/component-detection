using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ComponentDetection.Common
{
    public static class AsyncExecution
    {
        public static async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> toExecute, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (toExecute == null)
            {
                throw new ArgumentNullException(nameof(toExecute));
            }

            var work = Task.Run(toExecute);

            var completedInTime = await Task.Run(() => work.Wait(timeout));
            if (!completedInTime)
            {
                throw new TimeoutException($"The execution did not complete in the alotted time ({timeout.TotalSeconds} seconds) and has been terminated prior to completion");
            }

            return work.Result;
        }

        public static async Task ExecuteVoidWithTimeoutAsync(Action toExecute, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (toExecute == null)
            {
                throw new ArgumentNullException(nameof(toExecute));
            }

            var work = Task.Run(toExecute);
            var completedInTime = await Task.Run(() => work.Wait(timeout));
            if (!completedInTime)
            {
                throw new TimeoutException($"The execution did not complete in the alotted time ({timeout.TotalSeconds} seconds) and has been terminated prior to completion");
            }
        }
    }
}