#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Tests;

using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class DetectedComponentTests
{
    [TestMethod]
    public void AddComponentFilePath_AddsPathsCorrectly()
    {
        var componentName = "express";
        var componentVersion = "1.0.0";
        var filePathToAdd = @"C:\some\fake\file\path.txt";

        var component = new DetectedComponent(new NpmComponent(componentName, componentVersion));

        component.FilePaths.Should().NotBeNull().And.HaveCount(0);

        component.AddComponentFilePath(filePathToAdd);

        component.FilePaths.Should().ContainSingle().And.Contain(filePathToAdd);
    }
}
