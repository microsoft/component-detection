using System.Collections.Generic;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.Mappers;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Contracts.Tests.Mappers
{
    [TestClass]
    public class CycloneDx
    {
        [TestMethod]
        public void ToCycloneDx_HappyPath()
        {
            var scanResult = new ScanResult
            {
                ComponentsFound = new List<ScannedComponent>
                {
                    new ScannedComponent
                    {
                        Component = new NpmComponent("lodash", "1.2.3"),
                        LocationsFoundAt = new[]
                        {
                            "/src/lodash.js",
                        },
                    },
                },
            };

            var result = scanResult.ToCycloneDxString();

            result.Should().NotBeEmpty();
        }
    }
}
