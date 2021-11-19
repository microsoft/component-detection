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
            string componentName = "express";
            string componentVersion = "1.0.0";
            string filePathToAdd = @"C:\some\fake\file\path.txt";

            DetectedComponent component = new DetectedComponent(new NpmComponent(componentName, componentVersion));

            Assert.IsNotNull(component.FilePaths);
            Assert.AreEqual(0, component.FilePaths.Count);

            component.AddComponentFilePath(filePathToAdd);

            Assert.AreEqual(1, component.FilePaths.Count);
            Assert.IsTrue(component.FilePaths.Contains(filePathToAdd));
        }
    }
}
