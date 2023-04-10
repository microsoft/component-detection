namespace Microsoft.ComponentDetection.Detectors.Tests.Utilities;

using System;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

internal class TestLogger<T> : ILogger<T>, IDisposable
{
    private readonly TestContext context;

    public TestLogger(TestContext context)
        => this.context = context;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => this;

    public void Dispose()
    {
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        => this.context.WriteLine($"{logLevel} ({eventId}): {formatter(state, exception)}");
}
