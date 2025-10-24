#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
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
public class SimplePyPiClientTests
{
    private Mock<HttpMessageHandler> MockHttpMessageHandler(string content, HttpStatusCode statusCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(content),
            });

        return handlerMock;
    }

    private ISimplePyPiClient CreateSimplePypiClient(HttpMessageHandler messageHandler, IEnvironmentVariableService evs, ILogger<SimplePyPiClient> logger)
    {
        SimplePyPiClient.HttpClient = new HttpClient(messageHandler);
        return new SimplePyPiClient(evs, logger);
    }

    [TestMethod]
    public async Task GetSimplePypiProject_DuplicateEntries_CallsGetAsync_OnceAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"], Name = "boto3" };
        var pythonProject = this.SampleValidApiJsonResponse("boto3", "0.0.1");
        var mockHandler = this.MockHttpMessageHandler(pythonProject, HttpStatusCode.OK);

        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);
        var action = async () => await simplePypiClient.GetSimplePypiProjectAsync(pythonSpecs);

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
    public async Task GetSimplePypiProject_DifferentEntries_CallsGetAsync_TwiceAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };
        var pythonProject = new SimplePypiProject()
        {
            Files = [new SimplePypiProjectRelease()],
        };

        var mockHandler = this.MockHttpMessageHandler(JsonConvert.SerializeObject(pythonProject), HttpStatusCode.OK);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var action = async () =>
        {
            pythonSpecs.Name = Guid.NewGuid().ToString();
            await simplePypiClient.GetSimplePypiProjectAsync(pythonSpecs);
        };

        await action.Should().NotThrowAsync();
        await action.Should().NotThrowAsync();

        // Verify the API call was performed twice
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task GetSimplePypiProject_ReturnsValidSimplePypiProjectAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"], Name = "boto3" };
        var sampleApiResponse = this.SampleValidApiJsonResponse("boto3", "0.0.1");
        var expectedResult = new SimplePypiProject()
        {
            Files =
            [
                new SimplePypiProjectRelease() { FileName = "boto3-0.0.1-py2.py3-none-any.whl", Url = new Uri("https://files.pythonhosted.org/packages/3f/95/a24847c245befa8c50a9516cbdca309880bd21b5879e7c895e953217e947/boto3-0.0.1-py2.py3-none-any.whl"), Size = 45469 },
                new SimplePypiProjectRelease() { FileName = "boto3-0.0.1.tar.gz", Url = new Uri("https://files.pythonhosted.org/packages/df/18/4e36b93f6afb79b5f67b38f7d235773a21831b193602848c590f8a008608/boto3-0.0.1.tar.gz"), Size = 33415 },
            ],
        };

        var mockHandler = this.MockHttpMessageHandler(sampleApiResponse, HttpStatusCode.OK);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var actualResult = await simplePypiClient.GetSimplePypiProjectAsync(pythonSpecs);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    public async Task GetSimplePypiProject_InvalidSpec_NotThrowAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"], Name = "randomName" };

        var mockHandler = this.MockHttpMessageHandler("404 Not Found", HttpStatusCode.NotFound);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var action = async () => await simplePypiClient.GetSimplePypiProjectAsync(pythonSpecs);

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task GetSimplePypiProject_UnexpectedContentTypeReturnedByApi_NotThrowAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };

        var content = "<!DOCTYPE html><body>\r\n\t<h1>Links for boto3</h1>\r\n\t<a\r\n\t\thref=\"some link\">boto3-0.0.1-py2.py3-none-any.whl</a><br /></html>";
        var mockHandler = this.MockHttpMessageHandler(content, HttpStatusCode.OK);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var action = async () => await simplePypiClient.GetSimplePypiProjectAsync(pythonSpecs);

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task GetSimplePypiProject_ShouldRetryAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };

        var mockHandler = this.MockHttpMessageHandler(string.Empty, HttpStatusCode.InternalServerError);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var action = async () => await simplePypiClient.GetSimplePypiProjectAsync(pythonSpecs);

        await action.Should().NotThrowAsync();

        // Verify the API call was retried max retry times
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly((int)SimplePyPiClient.MAXRETRIES),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task GetSimplePypiProject_ShouldNotRetryAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };
        var mockHandler = this.MockHttpMessageHandler("some content", HttpStatusCode.MultipleChoices);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var action = async () => await simplePypiClient.GetSimplePypiProjectAsync(pythonSpecs);
        await action.Should().NotThrowAsync();

        // Verify the API call was called only once
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task GetSimplePypiProject_AddsCorrectHeadersAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };
        var pythonProject = this.SampleValidApiJsonResponse("boto3", "0.0.1");

        var mockHandler = this.MockHttpMessageHandler(pythonProject, HttpStatusCode.OK);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var action = async () => await simplePypiClient.GetSimplePypiProjectAsync(pythonSpecs);

        await action.Should().NotThrowAsync();

        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(
                req => req.Headers.UserAgent.Count == 2 &&
                       req.Headers.UserAgent.First().Product.Name == "ComponentDetection"
                       && req.Headers.UserAgent.Last().Comment == "(+https://github.com/microsoft/component-detection)"
                       && req.Headers.Accept.First().ToString() == "application/vnd.pypi.simple.v1+json"),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task GetSimplePypiProject_MaxEntriesVariable_CreatesNewCacheAsync()
    {
        var pythonSpecs = new PipDependencySpecification { DependencySpecifiers = ["==1.0.0"] };
        var pythonProject = this.SampleValidApiJsonResponse("boto3", "0.0.1");
        var mockHandler = this.MockHttpMessageHandler(pythonProject, HttpStatusCode.OK);

        var mockLogger = new Mock<ILogger<SimplePyPiClient>>();
        var mockEvs = new Mock<IEnvironmentVariableService>();
        mockEvs.Setup(x => x.GetEnvironmentVariable(It.Is<string>(s => s.Equals("PyPiMaxCacheEntries")))).Returns("32");

        var simplePyPiClient = this.CreateSimplePypiClient(mockHandler.Object, mockEvs.Object, mockLogger.Object);

        var action = async () => await simplePyPiClient.GetSimplePypiProjectAsync(pythonSpecs);

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
    public async Task FetchPackageFileStream_MaxEntriesVariable_CreatesNewCacheAsync()
    {
        var mockHandler = this.MockHttpMessageHandler(string.Empty, HttpStatusCode.OK);

        var mockLogger = new Mock<ILogger<SimplePyPiClient>>();
        var mockEvs = new Mock<IEnvironmentVariableService>();
        mockEvs.Setup(x => x.GetEnvironmentVariable(It.Is<string>(s => s.Equals("PyPiMaxCacheEntries")))).Returns("32");

        var mockedPyPi = this.CreateSimplePypiClient(mockHandler.Object, mockEvs.Object, mockLogger.Object);

        var action = async () => await mockedPyPi.FetchPackageFileStreamAsync(new Uri($"https://testurl"));

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
    public async Task FetchPackageFileStream_DuplicateEntries_CallsGetAsync_OnceAsync()
    {
        var mockHandler = this.MockHttpMessageHandler(string.Empty, HttpStatusCode.OK);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var action = async () => await simplePypiClient.FetchPackageFileStreamAsync(new Uri($"https://testurl"));

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
    public async Task FetchPackageFileStream_DifferentEntries_CallsGetAsync_TwiceAsync()
    {
        var mockHandler = this.MockHttpMessageHandler(string.Empty, HttpStatusCode.OK);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var action = async () => await simplePypiClient.FetchPackageFileStreamAsync(new Uri($"https://{Guid.NewGuid()}"));

        await action.Should().NotThrowAsync();
        await action.Should().NotThrowAsync();

        // Verify the API call was performed twice
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task FetchPackageFileStream_UnableToRetrievePackageAsync()
    {
        var mockHandler = this.MockHttpMessageHandler(string.Empty, HttpStatusCode.InternalServerError);
        var simplePypiClient = this.CreateSimplePypiClient(mockHandler.Object, new Mock<EnvironmentVariableService>().Object, new Mock<ILogger<SimplePyPiClient>>().Object);

        var action = async () => { return await simplePypiClient.FetchPackageFileStreamAsync(new Uri($"https://{Guid.NewGuid()}")); };

        await action.Should().NotThrowAsync();
    }

    public string SampleValidApiJsonResponse(string packageName, string version)
    {
        var packageJson = @"{{
                ""files"": [
        {{
            ""core-metadata"": false,
            ""data-dist-info-metadata"": false,
            ""filename"": ""{0}-{1}-py2.py3-none-any.whl"",
            ""hashes"": {{
                ""sha256"": ""bc9b3ce78d3863e45b43a33d076c7b0561f6590205c94f0f8a23a4738e79a13f""
            }},
            ""requires-python"": null,
            ""size"": 45469,
            ""upload-time"": ""2014-11-11T20:30:49.562183Z"",
            ""url"": ""https://files.pythonhosted.org/packages/3f/95/a24847c245befa8c50a9516cbdca309880bd21b5879e7c895e953217e947/{0}-{1}-py2.py3-none-any.whl"",
            ""yanked"": false
        }},
        {{
            ""core-metadata"": false,
            ""data-dist-info-metadata"": false,
            ""filename"": ""{0}-{1}.tar.gz"",
            ""hashes"": {{
                ""sha256"": ""bc018a3aedc5cf7329dcdeb435ece8a296b605c19fb09842c1821935f1b14cfd""
            }},
            ""requires-python"": null,
            ""size"": 33415,
            ""upload-time"": ""2014-11-11T20:30:40.057636Z"",
            ""url"": ""https://files.pythonhosted.org/packages/df/18/4e36b93f6afb79b5f67b38f7d235773a21831b193602848c590f8a008608/{0}-{1}.tar.gz"",
            ""yanked"": false
        }}
    ],
    ""meta"": {{
        ""_last-serial"": 18925095,
        ""api-version"": ""1.1""
    }},
    ""name"": ""{0}"",
    ""versions"": [
        ""{1}""
    ]
            }}";

        var packageJsonTemplate = string.Format(packageJson, packageName, version);

        return packageJsonTemplate;
    }
}
