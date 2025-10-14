#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Commands;

using System.IO;
using FluentAssertions;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class BaseSettingsTests
{
    [TestMethod]
    public void Validate_FailsNegativeTimeout()
    {
        var settings = new TestBaseSettings
        {
            Timeout = -1,
        };

        var result = settings.Validate();

        result.Successful.Should().BeFalse();
    }

    [TestMethod]
    public void Validate_Success_Empty_Output()
    {
        var settings = new TestBaseSettings
        {
            Output = string.Empty,
        };

        var result = settings.Validate();

        result.Successful.Should().BeTrue();
    }

    [TestMethod]
    public void Validate_Fails_Output_NotExists()
    {
        var setting = new TestBaseSettings
        {
            Output = Path.Combine(Path.GetTempPath(), Path.GetTempFileName()),
        };

        var result = setting.Validate();

        result.Successful.Should().BeFalse();
    }

    private class TestBaseSettings : BaseSettings
    {
    }
}
