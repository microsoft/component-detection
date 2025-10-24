#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Commands;

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Orchestrator.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Spectre.Console.Testing;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ListDetectorCommandTests
{
    [TestMethod]
    public void ListDetectorCommands_ExecutesListDetectors()
    {
        using var console = new TestConsole();
        var fakeIds = Enumerable.Range(0, 10).Select(x => $"Detector{x}").ToList();
        var mockDetectors = new List<IComponentDetector>(10);
        foreach (var id in fakeIds)
        {
            var mock = new Mock<IComponentDetector>();
            mock.Setup(x => x.Id).Returns(id);
            mockDetectors.Add(mock.Object);
        }

        var command = new ListDetectorsCommand(mockDetectors, console);

        var result = command.Execute(null, new ListDetectorsSettings());

        result.Should().Be(0);
        console.Output.Should().ContainAll(fakeIds);
    }
}
