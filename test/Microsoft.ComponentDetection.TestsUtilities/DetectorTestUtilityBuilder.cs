#nullable disable
namespace Microsoft.ComponentDetection.TestsUtilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

public class DetectorTestUtilityBuilder<T>
    where T : FileComponentDetector
{
    private readonly List<(string Name, Stream Contents, string Location, IEnumerable<string> SearchPatterns)>
        filesToAdd = [];

    private readonly Mock<IComponentStreamEnumerableFactory> mockComponentStreamEnumerableFactory;
    private readonly Mock<IObservableDirectoryWalkerFactory> mockObservableDirectoryWalkerFactory;
    private readonly Mock<ILogger<T>> mockLogger;

    private readonly IServiceCollection serviceCollection;
    private T detector;
    private ScanRequest scanRequest;
    private IComponentRecorder componentRecorder = new ComponentRecorder();

    public DetectorTestUtilityBuilder()
    {
        this.serviceCollection = new ServiceCollection()
            .AddSingleton<T>();

        this.mockComponentStreamEnumerableFactory = new Mock<IComponentStreamEnumerableFactory>();
        this.serviceCollection.AddSingleton(_ =>
            this.mockComponentStreamEnumerableFactory?.Object);

        this.mockObservableDirectoryWalkerFactory = new Mock<IObservableDirectoryWalkerFactory>();
        this.serviceCollection.AddSingleton(_ =>
            this.mockObservableDirectoryWalkerFactory?.Object);

        this.mockLogger = new Mock<ILogger<T>>();
        this.serviceCollection.AddSingleton(_ =>
            this.mockLogger?.Object);

        this.serviceCollection.AddSingleton(_ => new Mock<IFileUtilityService>().Object);
        this.serviceCollection.AddSingleton(_ => new Mock<IDirectoryUtilityService>().Object);
    }

    public DetectorTestUtilityBuilder<T> WithFile(string fileName, string fileContents, IEnumerable<string> searchPatterns = null, string fileLocation = null) =>
        this.WithFile(fileName, fileContents.ToStream(), searchPatterns, fileLocation);

    public DetectorTestUtilityBuilder<T> AddServiceMock<TMock>(Mock<TMock> mock)
        where TMock : class
    {
        this.serviceCollection.AddSingleton(_ => mock.Object);
        return this;
    }

    public DetectorTestUtilityBuilder<T> AddService<TService>(TService service)
        where TService : class
    {
        this.serviceCollection.AddSingleton(_ => service);
        return this;
    }

    public DetectorTestUtilityBuilder<T> WithFile(string fileName, Stream fileContents, IEnumerable<string> searchPatterns = null, string fileLocation = null)
    {
        if (string.IsNullOrEmpty(fileLocation))
        {
            fileLocation = Path.Combine(Path.GetTempPath(), fileName);
        }

        this.filesToAdd.Add((fileName, fileContents, fileLocation, searchPatterns));

        return this;
    }

    public DetectorTestUtilityBuilder<T> WithScanRequest(ScanRequest scanRequest)
    {
        this.scanRequest = scanRequest;
        return this;
    }

    public async Task<(IndividualDetectorScanResult ScanResult, IComponentRecorder ComponentRecorder)> ExecuteDetectorAsync()
    {
        if (this.scanRequest is null)
        {
            this.scanRequest = new ScanRequest(
                new DirectoryInfo(Path.GetTempPath()),
                null,
                null,
                new Dictionary<string, string>(),
                null,
                this.componentRecorder,
                sourceFileRoot: new DirectoryInfo(Path.GetTempPath()));
        }
        else
        {
            this.componentRecorder = this.scanRequest.ComponentRecorder;
        }

        this.detector = this.serviceCollection.BuildServiceProvider()
            .GetRequiredService<T>();

        this.InitializeFileMocks();

        var result = await this.detector.ExecuteDetectorAsync(this.scanRequest);
        return (result, this.componentRecorder);
    }

    private static IComponentStream CreateComponentStreamForFile(string pattern, string filePath, Stream content)
    {
        var getFileMock = new Mock<IComponentStream>();
        getFileMock.SetupGet(x => x.Stream).Returns(content);
        getFileMock.SetupGet(x => x.Pattern).Returns(pattern);
        getFileMock.SetupGet(x => x.Location).Returns(filePath);

        return getFileMock.Object;
    }

    private static string FindMatchingPattern(string fileName, IEnumerable<string> searchPatterns)
    {
        var foundPattern = searchPatterns.FirstOrDefault(searchPattern => new PathUtilityService(null).MatchesPattern(searchPattern, fileName));

        return foundPattern ?? fileName;
    }

    private ProcessRequest CreateProcessRequest(string pattern, string filePath, Stream content) =>
        new()
        {
            SingleFileComponentRecorder = this.componentRecorder.CreateSingleFileComponentRecorder(filePath),
            ComponentStream = CreateComponentStreamForFile(pattern, filePath, content),
        };

    private void InitializeFileMocks()
    {
        if (this.filesToAdd.Count == 0)
        {
            this.mockObservableDirectoryWalkerFactory.Setup(x =>
                    x.GetFilteredComponentStreamObservable(
                        It.IsAny<DirectoryInfo>(),
                        this.detector.SearchPatterns ?? this.detector.SearchPatterns,
                        It.IsAny<IComponentRecorder>()))
                .Returns(Enumerable.Empty<ProcessRequest>().ToObservable());
        }

        if (this.filesToAdd.Count == 0)
        {
            this.mockComponentStreamEnumerableFactory.Setup(x =>
                    x.GetComponentStreams(
                        It.IsAny<DirectoryInfo>(),
                        this.detector.SearchPatterns ?? this.detector.SearchPatterns,
                        It.IsAny<ExcludeDirectoryPredicate>(),
                        It.IsAny<bool>()))
                .Returns(Enumerable.Empty<ComponentStream>());
        }

        var filesGroupedBySearchPattern =
            this.filesToAdd.GroupBy(f => f.SearchPatterns ?? this.detector.SearchPatterns, new EnumerableStringComparer());
        foreach (var group in filesGroupedBySearchPattern)
        {
            var searchPatterns = group.Key;
            var filesToSend = group.Select(grouping => (grouping.Name, grouping.Contents, grouping.Location));

            this.mockObservableDirectoryWalkerFactory.Setup(x =>
                    x.GetFilteredComponentStreamObservable(
                        It.IsAny<DirectoryInfo>(),
                        searchPatterns,
                        It.IsAny<IComponentRecorder>()))
                .Returns<DirectoryInfo, IEnumerable<string>, IComponentRecorder>(
                    (_, searchPatterns, _) =>
                        filesToSend
                            .Select(fileToSend =>
                                this.CreateProcessRequest(
                                    FindMatchingPattern(fileToSend.Name, searchPatterns),
                                    fileToSend.Location,
                                    fileToSend.Contents)).ToObservable());

            this.mockComponentStreamEnumerableFactory.Setup(x =>
                    x.GetComponentStreams(
                        It.IsAny<DirectoryInfo>(),
                        searchPatterns,
                        It.IsAny<ExcludeDirectoryPredicate>(),
                        It.IsAny<bool>()))
                .Returns<DirectoryInfo, IEnumerable<string>, ExcludeDirectoryPredicate, bool>(
                    (directoryInfo, searchPatterns, _, recurse) =>
                    {
                        if (recurse)
                        {
                            return filesToSend
                                .Select(fileToSend =>
                                    this.CreateProcessRequest(
                                        FindMatchingPattern(
                                            fileToSend.Name,
                                            searchPatterns),
                                        fileToSend.Location,
                                        fileToSend.Contents)).Select(pr => pr.ComponentStream);
                        }

                        return filesToSend
                            .Where(fileToSend =>
                                Directory.GetParent(fileToSend.Location).FullName == directoryInfo.FullName)
                            .Select(fileToSend =>
                                this.CreateProcessRequest(
                                    FindMatchingPattern(
                                        fileToSend.Name,
                                        searchPatterns),
                                    fileToSend.Location,
                                    fileToSend.Contents)).Select(pr => pr.ComponentStream);
                    });

            this.mockComponentStreamEnumerableFactory.Setup(x =>
                    x.GetComponentStreams(
                        It.IsAny<DirectoryInfo>(),
                        It.IsAny<Func<FileInfo, bool>>(),
                        It.IsAny<ExcludeDirectoryPredicate>(),
                        It.IsAny<bool>()))
                .Returns<DirectoryInfo, Func<FileInfo, bool>, ExcludeDirectoryPredicate, bool>(
                    (directoryInfo, fileMatchingPredicate, _, recurse) =>
                    {
                        return filesToSend
                            .Where(fileToSend => fileMatchingPredicate(new FileInfo(fileToSend.Location)))
                            .Select(fileToSend =>
                                this.CreateProcessRequest(
                                    FindMatchingPattern(
                                        fileToSend.Name,
                                        searchPatterns),
                                    fileToSend.Location,
                                    fileToSend.Contents)).Select(pr => pr.ComponentStream);
                    });
        }
    }
}
