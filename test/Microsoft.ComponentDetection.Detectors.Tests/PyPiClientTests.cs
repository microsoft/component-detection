#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;

[TestClass]
public class PyPiClientTests
{
    private readonly PyPiClient pypiClient;

    public PyPiClientTests() => this.pypiClient = new PyPiClient(
            new EnvironmentVariableService(),
            new Mock<ILogger<PyPiClient>>().Object);

    [TestMethod]
    public async Task GetReleases_InvalidSpecVersion_NotThrowAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0", "==1.0.0notvalid"] };

        var pythonProject = new PythonProject
        {
            Releases = new SortedDictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        Func<Task> action = async () => await this.pypiClient.GetProjectAsync(pythonSpecs);

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task GetProject_SupportsReleaseCandidatesDependenciesAsync()
    {
        const string version = "1.0.0rc4";
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = [$"=={version}"] };

        var pythonProject = new PythonProject
        {
            Releases = new SortedDictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
                { version, new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        var result = await this.pypiClient.GetProjectAsync(pythonSpecs);
        result.Releases.Should().ContainSingle();
        result.Releases.Keys.First().Should().Be(version);
    }

    [TestMethod]
    public async Task GetReleases_DuplicateEntries_CallsGetAsync_OnceAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };
        var pythonProject = new PythonProject
        {
            Releases = new SortedDictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        Func<Task> action = async () => await this.pypiClient.GetProjectAsync(pythonSpecs);

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
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };
        var pythonProject = new PythonProject
        {
            Releases = new SortedDictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        Func<Task> action = async () =>
        {
            pythonSpecs.Name = Guid.NewGuid().ToString();
            await this.pypiClient.GetProjectAsync(pythonSpecs);
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
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };
        var pythonProject = new PythonProject
        {
            Releases = new SortedDictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        var mockLogger = new Mock<ILogger<PyPiClient>>();
        var mockEvs = new Mock<IEnvironmentVariableService>();
        mockEvs.Setup(x => x.GetEnvironmentVariable(It.Is<string>(s => s.Equals("PyPiMaxCacheEntries")))).Returns("32");

        var mockedPyPi = new PyPiClient(
            mockEvs.Object,
            mockLogger.Object);

        Func<Task> action = async () => await mockedPyPi.GetProjectAsync(pythonSpecs);

        await action.Should().NotThrowAsync();
        await action.Should().NotThrowAsync();

        // Verify the cache setup call was performed only once
        mockEvs.Verify(x => x.GetEnvironmentVariable(It.IsAny<string>()), Times.Once());
        mockLogger.Verify(
            x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
            Times.Exactly(3));
    }

    [TestMethod]
    public async Task GetReleases_AddsUserAgentHeadersAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };
        var pythonProject = new PythonProject
        {
            Releases = new SortedDictionary<string, IList<PythonProjectRelease>>
            {
                { "1.0.0", new List<PythonProjectRelease> { new PythonProjectRelease() } },
            },
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject));
        PyPiClient.HttpClient = new HttpClient(mockHandler.Object);

        var action = async () => await this.pypiClient.GetProjectAsync(pythonSpecs);

        await action.Should().NotThrowAsync();

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(
                req => req.Headers.UserAgent.Count == 2 &&
                       req.Headers.UserAgent.First().Product.Name == "ComponentDetection"
                       && req.Headers.UserAgent.Last().Comment == "(+https://github.com/microsoft/component-detection)"),
            ItExpr.IsAny<CancellationToken>());
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
