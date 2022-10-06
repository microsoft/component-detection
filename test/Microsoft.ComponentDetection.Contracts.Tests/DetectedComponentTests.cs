using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Contracts.Tests
{
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

            Assert.IsNotNull(component.FilePaths);
            Assert.AreEqual(0, component.FilePaths.Count);

            component.AddComponentFilePath(filePathToAdd);

            Assert.AreEqual(1, component.FilePaths.Count);
            Assert.IsTrue(component.FilePaths.Contains(filePathToAdd));
        }
    }
}
