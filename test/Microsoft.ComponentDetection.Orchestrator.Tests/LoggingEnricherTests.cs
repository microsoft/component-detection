#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests;

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Serilog.Core;
using Serilog.Events;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class LoggingEnricherTests
{
    private LoggingEnricher enricher;
    private Mock<ILogEventPropertyFactory> propertyFactoryMock;

    [TestInitialize]
    public void TestInitialize()
    {
        this.propertyFactoryMock = new Mock<ILogEventPropertyFactory>();
        this.propertyFactoryMock.Setup(x => x.CreateProperty(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
            .Returns<string, object, bool>((name, value, _) => new LogEventProperty(name, new ScalarValue(value)));
        this.enricher = new LoggingEnricher();
    }

    [TestMethod]
    public void EnrichPath_Works()
    {
        this.TestLogEventProperty(LoggingEnricher.LogFilePathPropertyName, LoggingEnricher.Path, string.Empty);
        LoggingEnricher.Path = "path";
        this.TestLogEventProperty(LoggingEnricher.LogFilePathPropertyName, LoggingEnricher.Path, "path");
    }

    [TestMethod]
    public void EnrichStderr_Works()
    {
        this.TestLogEventProperty(LoggingEnricher.PrintStderrPropertyName, LoggingEnricher.PrintStderr, false);
        LoggingEnricher.PrintStderr = true;
        this.TestLogEventProperty(LoggingEnricher.PrintStderrPropertyName, LoggingEnricher.PrintStderr, true);
    }

    private void TestLogEventProperty<T>(string propertyName, T propertyValue, T expected)
    {
        var logEvent = new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Debug,
            null,
            new MessageTemplate("test", []),
            []);

        this.enricher.Enrich(logEvent, this.propertyFactoryMock.Object);

        this.propertyFactoryMock.Verify(
            x => x.CreateProperty(propertyName, propertyValue, false), Times.Once);
        var scalarValue = logEvent.Properties[propertyName];
        scalarValue.Should().BeAssignableTo<ScalarValue>();

        var value = (ScalarValue)scalarValue;
        value.Value.Should().BeAssignableTo<T>();
        value.Value.Should().Be(expected);
    }
}
