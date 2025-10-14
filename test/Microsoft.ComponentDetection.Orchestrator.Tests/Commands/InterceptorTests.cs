#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Commands;

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.Telemetry;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Serilog.Events;
using Spectre.Console.Cli;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class InterceptorTests
{
    private Mock<ITypeResolver> typeResolverMock;
    private Interceptor interceptor;
    private Mock<ITelemetryService> telemServiceMock;

    [TestInitialize]
    public void TestInitialize()
    {
        this.typeResolverMock = new Mock<ITypeResolver>();
        this.telemServiceMock = new Mock<ITelemetryService>();
        this.typeResolverMock
            .Setup(x => x.Resolve(typeof(IEnumerable<ITelemetryService>)))
            .Returns(new[] { this.telemServiceMock.Object });
        this.interceptor = new Interceptor(this.typeResolverMock.Object);
    }

    [TestMethod]
    public void Intercepts_BaseSettings_DebugTelem()
    {
        var settings = new ScanSettings()
        {
            LogLevel = LogEventLevel.Debug,
            Output = "output",
            DebugTelemetry = true,
            PrintManifest = true,
        };

        this.interceptor.Intercept(null, settings);

        Interceptor.LogLevel.MinimumLevel.Should().Be(LogEventLevel.Debug);
        LoggingEnricher.Path.Should().StartWith("output");
        this.telemServiceMock.Verify(x => x.SetMode(TelemetryMode.Debug), Times.Once());
        LoggingEnricher.PrintStderr.Should().BeTrue();
    }
}
