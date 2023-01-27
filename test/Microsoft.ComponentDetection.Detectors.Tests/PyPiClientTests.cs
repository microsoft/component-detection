namespace Microsoft.ComponentDetection.Detectors.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;

[TestClass]
public class PyPiClientTests
{
    private PyPiClient pypiClient;

    [TestInitialize]
    public void Initialize() => this.pypiClient = new PyPiClient()
    {
        EnvironmentVariableService = new EnvironmentVariableService(),
        Logger = new Mock<ILogger>().Object,
    };

    [TestMethod]
    public async Task GetReleases_InvalidSpecVersion_NotThrowAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = new List<string> { "==1.0.0", "==1.0.0notvalid" } };

        var pythonProject = new PythonProject
        {
            Releases = new Dictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        Func<Task> action = async () => await this.pypiClient.GetReleasesAsync(pythonSpecs);

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task GetReleases_DuplicateEntries_CallsGetAsync_OnceAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = new List<string> { "==1.0.0" } };
        var pythonProject = new PythonProject
        {
            Releases = new Dictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        Func<Task> action = async () => await this.pypiClient.GetReleasesAsync(pythonSpecs);

        await action.Should().NotThrowAsync();
        await action.Should().NotThrowAsync();

        // Verify the API call was performed only once
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task GetReleases_DifferentEntries_CallsGetAsync_OnceAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = new List<string> { "==1.0.0" } };
        var pythonProject = new PythonProject
        {
            Releases = new Dictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        Func<Task> action = async () =>
        {
            pythonSpecs.Name = Guid.NewGuid().ToString();
            await this.pypiClient.GetReleasesAsync(pythonSpecs);
        };

        await action.Should().NotThrowAsync();
        await action.Should().NotThrowAsync();

        // Verify the API call was performed only once
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task FetchPackageDependencies_DuplicateEntries_CallsGetAsync_OnceAsync()
    {
        var mockHandler = this.MockHttpMessageHandler("invalid ZIP");
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        Func<Task> action = async () => await this.pypiClient.FetchPackageDependenciesAsync("a", "1.0.0", new PythonProjectRelease { PackageType = "bdist_wheel", PythonVersion = "3.5.2", Size = 1000, Url = new Uri($"https://testurl") });

        await action.Should().ThrowAsync<InvalidDataException>();
        await action.Should().ThrowAsync<InvalidDataException>();

        // Verify the API call was performed only once
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task FetchPackageDependencies_DifferentEntries_CallsGetAsync_OnceAsync()
    {
        var mockHandler = this.MockHttpMessageHandler("invalid ZIP");
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        Func<Task> action = async () => await this.pypiClient.FetchPackageDependenciesAsync("a", "1.0.0", new PythonProjectRelease { PackageType = "bdist_wheel", PythonVersion = "3.5.2", Size = 1000, Url = new Uri($"https://{Guid.NewGuid()}") });

        await action.Should().ThrowAsync<InvalidDataException>();
        await action.Should().ThrowAsync<InvalidDataException>();

        // Verify the API call was performed only once
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task GetReleases_MaxEntriesVariable_CreatesNewCacheAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = new List<string> { "==1.0.0" } };
        var pythonProject = new PythonProject
        {
            Releases = new Dictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        var mockLogger = new Mock<ILogger>();
        var mockEvs = new Mock<IEnvironmentVariableService>();
        mockEvs.Setup(x => x.GetEnvironmentVariable(It.Is<string>(s => s.Equals("PyPiMaxCacheEntries")))).Returns("32");

        var mockedPyPi = new PyPiClient()
        {
            EnvironmentVariableService = mockEvs.Object,
            Logger = mockLogger.Object,
        };

        Func<Task> action = async () => await mockedPyPi.GetReleasesAsync(pythonSpecs);

        await action.Should().NotThrowAsync();
        await action.Should().NotThrowAsync();

        // Verify the cache setup call was performed only once
        mockEvs.Verify(x => x.GetEnvironmentVariable(It.IsAny<string>()), Times.Once());
        mockLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Equals("Setting IPyPiClient max cache entries to 32"))), Times.Once());
    }

    private Mock<HttpMessageHandler> MockHttpMessageHandler(string content)
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

        return handlerMock;
    }
}
