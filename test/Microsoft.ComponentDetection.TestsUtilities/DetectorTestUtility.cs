using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;

namespace Microsoft.ComponentDetection.TestsUtilities
{
    public class DetectorTestUtility<T>
        where T : FileComponentDetector, new()
    {
        private Mock<ILogger> mockLogger = new Mock<ILogger>();

        private Mock<IComponentStreamEnumerableFactory> mockComponentStreamEnumerableFactory;

        private Mock<IObservableDirectoryWalkerFactory> mockObservableDirectoryWalkerFactory;

        private IComponentRecorder componentRecorder = new ComponentRecorder();

        private ScanRequest scanRequest;

        private T detector;

        private List<(string Name, Stream Contents, string Location, IEnumerable<string> searchPatterns)> filesToAdd = new List<(string Name, Stream Contents, string Location, IEnumerable<string> searchPatterns)>();

        public async Task<(IndividualDetectorScanResult, IComponentRecorder)> ExecuteDetector()
        {
            if (scanRequest == null)
            {
                scanRequest = new ScanRequest(new DirectoryInfo(Path.GetTempPath()), null, null, new Dictionary<string, string>(), null, componentRecorder);
            }
            else
            {
                componentRecorder = scanRequest.ComponentRecorder;
            }

            InitializeFileRelatedMocksUsingDefaultImplementationIfNecessary();

            detector.Scanner = mockObservableDirectoryWalkerFactory.Object;
            detector.ComponentStreamEnumerableFactory = mockComponentStreamEnumerableFactory.Object;
            detector.Logger = mockLogger.Object;

            var scanResult = await detector.ExecuteDetectorAsync(scanRequest);
            return (scanResult, componentRecorder);
        }

        /// <summary>
        /// This is used to override specific services that certain detectors use which aren't common/shared between all detectors.
        /// For example: CommandLineExecutionService for the LinuxDetector and PathUtilityService for NpmWithRoots Detector.
        /// </summary>
        /// <param name="detector"></param>
        /// <returns></returns>
        public DetectorTestUtility<T> WithDetector(T detector)
        {
            this.detector = detector;
            return this;
        }

        public DetectorTestUtility<T> WithLogger(Mock<ILogger> mockLogger)
        {
            this.mockLogger = mockLogger;
            return this;
        }

        public DetectorTestUtility<T> WithScanRequest(ScanRequest scanRequest)
        {
            this.scanRequest = scanRequest;
            return this;
        }

        public DetectorTestUtility<T> WithFile(string fileName, string fileContents, IEnumerable<string> searchPatterns = null, string fileLocation = null)
        {
            return WithFile(fileName, fileContents.ToStream(), searchPatterns, fileLocation);
        }

        public DetectorTestUtility<T> WithFile(string fileName, Stream fileContents, IEnumerable<string> searchPatterns = null, string fileLocation = null)
        {
            if (string.IsNullOrEmpty(fileLocation))
            {
                fileLocation = Path.Combine(Path.GetTempPath(), fileName);
            }

            if (searchPatterns == null || !searchPatterns.Any())
            {
                searchPatterns = detector.SearchPatterns;
            }

            filesToAdd.Add((fileName, fileContents, fileLocation, searchPatterns));

            return this;
        }

        public DetectorTestUtility<T> WithComponentStreamEnumerableFactory(Mock<IComponentStreamEnumerableFactory> mock)
        {
            mockComponentStreamEnumerableFactory = mock;
            return this;
        }

        public DetectorTestUtility<T> WithObservableDirectoryWalkerFactory(Mock<IObservableDirectoryWalkerFactory> mock)
        {
            mockObservableDirectoryWalkerFactory = mock;
            return this;
        }

        private ProcessRequest CreateProcessRequest(string pattern, string filePath, Stream content)
        {
            return new ProcessRequest
            {
                SingleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(filePath),
                ComponentStream = CreateComponentStreamForFile(pattern, filePath, content),
            };
        }

        private static IComponentStream CreateComponentStreamForFile(string pattern, string filePath, Stream content)
        {
            var getFileMock = new Mock<IComponentStream>();
            getFileMock.SetupGet(x => x.Stream).Returns(content);
            getFileMock.SetupGet(x => x.Pattern).Returns(pattern);
            getFileMock.SetupGet(x => x.Location).Returns(filePath);

            return getFileMock.Object;
        }

        private void InitializeFileRelatedMocksUsingDefaultImplementationIfNecessary()
        {
            bool useDefaultObservableDirectoryWalkerFactory = false, useDefaultComponentStreamEnumerableFactory = false;

            if (mockObservableDirectoryWalkerFactory == null)
            {
                useDefaultObservableDirectoryWalkerFactory = true;
                mockObservableDirectoryWalkerFactory = new Mock<IObservableDirectoryWalkerFactory>();
            }

            if (mockComponentStreamEnumerableFactory == null)
            {
                useDefaultComponentStreamEnumerableFactory = true;
                mockComponentStreamEnumerableFactory = new Mock<IComponentStreamEnumerableFactory>();
            }

            if (!filesToAdd.Any() && useDefaultObservableDirectoryWalkerFactory)
            {
                mockObservableDirectoryWalkerFactory.Setup(x =>
                x.GetFilteredComponentStreamObservable(It.IsAny<DirectoryInfo>(), detector.SearchPatterns, It.IsAny<IComponentRecorder>()))
                    .Returns(Enumerable.Empty<ProcessRequest>().ToObservable());
            }

            if (!filesToAdd.Any() && useDefaultComponentStreamEnumerableFactory)
            {
                mockComponentStreamEnumerableFactory.Setup(x =>
                x.GetComponentStreams(It.IsAny<DirectoryInfo>(), detector.SearchPatterns, It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<bool>()))
                    .Returns(Enumerable.Empty<ComponentStream>());
            }

            var filesGroupedBySearchPattern = filesToAdd.GroupBy(filesToAdd => filesToAdd.searchPatterns, new EnumerableStringComparer());
            foreach (var group in filesGroupedBySearchPattern)
            {
                var searchPatterns = group.Key;
                var filesToSend = group.Select(grouping => (grouping.Name, grouping.Contents, grouping.Location));

                if (useDefaultObservableDirectoryWalkerFactory)
                {
                    mockObservableDirectoryWalkerFactory.Setup(x =>
                    x.GetFilteredComponentStreamObservable(It.IsAny<DirectoryInfo>(), searchPatterns, It.IsAny<IComponentRecorder>()))
                        .Returns<DirectoryInfo, IEnumerable<string>, IComponentRecorder>((directoryInfo, searchPatterns, componentRecorder) =>
                        {
                            return filesToSend
                                .Select(fileToSend => CreateProcessRequest(FindMatchingPattern(fileToSend.Name, searchPatterns), fileToSend.Location, fileToSend.Contents)).ToObservable();
                        });
                }

                if (useDefaultComponentStreamEnumerableFactory)
                {
                    mockComponentStreamEnumerableFactory.Setup(x =>
                    x.GetComponentStreams(It.IsAny<DirectoryInfo>(), searchPatterns, It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<bool>()))
                        .Returns<DirectoryInfo, IEnumerable<string>, ExcludeDirectoryPredicate, bool>((directoryInfo, searchPatterns, excludeDirectoryPredicate, recurse) =>
                        {
                            if (recurse)
                            {
                                return filesToSend
                                    .Select(fileToSend => CreateProcessRequest(FindMatchingPattern(fileToSend.Name, searchPatterns), fileToSend.Location, fileToSend.Contents)).Select(pr => pr.ComponentStream);
                            }
                            else
                            {
                                return filesToSend
                                    .Where(fileToSend => Directory.GetParent(fileToSend.Location).FullName == directoryInfo.FullName)
                                    .Select(fileToSend => CreateProcessRequest(FindMatchingPattern(fileToSend.Name, searchPatterns), fileToSend.Location, fileToSend.Contents)).Select(pr => pr.ComponentStream);
                            } 
                        });
                }
            }

            var providedDetectorSearchPatterns = filesGroupedBySearchPattern.Any(group => group.Key.SequenceEqual(detector.SearchPatterns));
            if (!providedDetectorSearchPatterns && useDefaultObservableDirectoryWalkerFactory)
            {
                mockObservableDirectoryWalkerFactory.Setup(x =>
                    x.GetFilteredComponentStreamObservable(It.IsAny<DirectoryInfo>(), detector.SearchPatterns, It.IsAny<IComponentRecorder>()))
                    .Returns(new List<ProcessRequest>().ToObservable());
            }

            if (!providedDetectorSearchPatterns && useDefaultComponentStreamEnumerableFactory)
            {
                mockComponentStreamEnumerableFactory.Setup(x =>
                    x.GetComponentStreams(It.IsAny<DirectoryInfo>(), detector.SearchPatterns, It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<bool>()))
                    .Returns(new List<IComponentStream>());
            }
        }

        private string FindMatchingPattern(string fileName, IEnumerable<string> searchPatterns)
        {
            var foundPattern = searchPatterns.Where(searchPattern => new PathUtilityService().MatchesPattern(searchPattern, fileName)).FirstOrDefault();

            return foundPattern != default(string) ? foundPattern : fileName;
        }
    }
}
