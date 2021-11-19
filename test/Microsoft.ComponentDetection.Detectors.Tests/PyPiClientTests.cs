using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    public class PyPiClientTests
    {
        private PyPiClient pypiClient;

        [TestInitialize]
        public void Initialize()
        {
            pypiClient = new PyPiClient()
            {
                Logger = new Mock<ILogger>().Object,
            };
        }

        [TestMethod]
        public async Task GetReleases_InvalidSpecVersion_NotThrow()
        {
            var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = new List<string> { "==1.0.0", "==1.0.0notvalid" } };

            var pythonProject = new PythonProject
            {
                Releases = new Dictionary<string, IList<PythonProjectRelease>>
                {
                    { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
                },
            };

            PyPiClient.HttpClient = new HttpClient(MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject)));

            Func<Task> action = async () => await pypiClient.GetReleases(pythonSpecs);

            await action.Should().NotThrowAsync();
        }

        private HttpMessageHandler MockHttpMessageHandler(string content)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(content),
               });

            return handlerMock.Object;
        }
    }
}
