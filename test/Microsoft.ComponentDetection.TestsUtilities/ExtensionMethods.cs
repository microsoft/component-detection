#nullable disable
namespace Microsoft.ComponentDetection.TestsUtilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;

public static class ExtensionMethods
{
    public static Stream ToStream(this string input)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        stream.Seek(0, SeekOrigin.Begin);

        return stream;
    }

    /// <summary>
    /// Sets up a mock logger to capture the log output to a <see cref="ICollection{T}"/>.
    /// </summary>
    /// <param name="mockLogger">The mock logger to save the output from.</param>
    /// <param name="logOutput">A collection to append the log output to. Each log is appended as an element in the collection.</param>
    /// <typeparam name="T">The type of the logger.</typeparam>
    public static void CaptureLogOutput<T>(this Mock<ILogger<T>> mockLogger, ICollection<string> logOutput) =>
        mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
            .Callback(new InvocationAction(invocation =>
            {
                var state = invocation.Arguments[2];
                var exception = (Exception)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];

                var invokeMethod = formatter.GetType().GetMethod("Invoke");
                var logMessage = (string)invokeMethod?.Invoke(formatter, [state, exception]);

                logOutput.Add(logMessage);
            }));
}
