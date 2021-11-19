using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.Pip;
using Microsoft.ComponentDetection.Orchestrator.ArgumentSets;
using Microsoft.ComponentDetection.Orchestrator.Services;
using Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.ComponentDetection.Orchestrator.Tests.Services
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class BcdeScanExecutionServiceTests
    {
        private Mock<ILogger> loggerMock;
        private Mock<IDetectorProcessingService> detectorProcessingServiceMock;
        private Mock<IDetectorRegistryService> detectorRegistryServiceMock;
        private Mock<IDetectorRestrictionService> detectorRestrictionServiceMock;
        private Mock<IComponentDetector> componentDetector2Mock;
        private Mock<IComponentDetector> componentDetector3Mock;
        private Mock<IComponentDetector> versionedComponentDetector1Mock;

        private DetectedComponent[] detectedComponents;
        private ContainerDetails sampleContainerDetails;

        private BcdeScanExecutionService serviceUnderTest;

        private DirectoryInfo sourceDirectory;
        private Dictionary<string, string> contentByFileInfo;

        [TestInitialize]
        public void InitializeTest()
        {
            loggerMock = new Mock<ILogger>();
            detectorProcessingServiceMock = new Mock<IDetectorProcessingService>();
            detectorRegistryServiceMock = new Mock<IDetectorRegistryService>();
            detectorRestrictionServiceMock = new Mock<IDetectorRestrictionService>();
            componentDetector2Mock = new Mock<IComponentDetector>();
            componentDetector3Mock = new Mock<IComponentDetector>();
            versionedComponentDetector1Mock = new Mock<IComponentDetector>();
            sampleContainerDetails = new ContainerDetails { Id = 1 };
            var defaultGraphTranslationService = new DefaultGraphTranslationService
            {
                Logger = loggerMock.Object,
            };

            contentByFileInfo = new Dictionary<string, string>();

            detectedComponents = new[]
            {
                new DetectedComponent(new NpmComponent("some-npm-component", "1.2.3")),
                new DetectedComponent(new NuGetComponent("SomeNugetComponent", "1.2.3.4")),
            };

            serviceUnderTest = new BcdeScanExecutionService
            {
                DetectorProcessingService = detectorProcessingServiceMock.Object,
                DetectorRegistryService = detectorRegistryServiceMock.Object,
                DetectorRestrictionService = detectorRestrictionServiceMock.Object,
                Logger = loggerMock.Object,
                GraphTranslationServices = new List<Lazy<IGraphTranslationService, GraphTranslationServiceMetadata>>
                {
                    new Lazy<IGraphTranslationService, GraphTranslationServiceMetadata>(() => defaultGraphTranslationService, new GraphTranslationServiceMetadata()),
                },
            };

            sourceDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

            sourceDirectory.Create();
        }

        [TestCleanup]
        public void CleanupTests()
        {
            detectorProcessingServiceMock.VerifyAll();
            detectorRegistryServiceMock.VerifyAll();
            detectorRestrictionServiceMock.VerifyAll();

            try
            {
                sourceDirectory.Delete(true);
            }
            catch
            {
            }
        }

        [TestMethod]
        public void DetectComponents_HappyPath()
        {
            var componentRecorder = new ComponentRecorder();
            var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(Path.Join(sourceDirectory.FullName, "/some/file/path"));

            componentDetector2Mock.SetupGet(x => x.Id).Returns("Detector2");
            componentDetector2Mock.SetupGet(x => x.Version).Returns(1);
            componentDetector3Mock.SetupGet(x => x.Id).Returns("Detector3");
            componentDetector3Mock.SetupGet(x => x.Version).Returns(10);

            detectedComponents[0].DevelopmentDependency = true;
            detectedComponents[0].ContainerDetailIds = new HashSet<int> { sampleContainerDetails.Id };
            singleFileComponentRecorder.RegisterUsage(detectedComponents[0], isDevelopmentDependency: true);

            var parentPipComponent = new PipComponent("sample-root", "1.0");
            detectedComponents[1].DependencyRoots = new HashSet<TypedComponent>(new[] { parentPipComponent });
            detectedComponents[1].DevelopmentDependency = null;
            singleFileComponentRecorder.RegisterUsage(new DetectedComponent(parentPipComponent, detector: new PipComponentDetector()), isExplicitReferencedDependency: true);
            singleFileComponentRecorder.RegisterUsage(detectedComponents[1], parentComponentId: parentPipComponent.Id);

            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };
            var result = DetectComponentsHappyPath(args, restrictions =>
            {
                restrictions.AllowedDetectorCategories.Should().BeNull();
                restrictions.AllowedDetectorIds.Should().BeNull();
            }, new List<ComponentRecorder> { componentRecorder });

            result.Result.Should().Be(ProcessingResultCode.Success);
            ValidateDetectedComponents(result.DetectedComponents);
            result.DetectorsInRun.Count().Should().Be(2);
            var detector2 = result.DetectorsInRun.Single(x => x.DetectorId == "Detector2");
            detector2.Version.Should().Be(1);
            var detector3 = result.DetectorsInRun.Single(x => x.DetectorId == "Detector3");
            detector3.Version.Should().Be(10);

            var npmComponent = result.DetectedComponents.Single(x => x.Component is NpmComponent);
            npmComponent.LocationsFoundAt.Count().Should().Be(1);
            npmComponent.LocationsFoundAt.First().Should().Be("/some/file/path");
            npmComponent.IsDevelopmentDependency.Should().Be(true);
            npmComponent.ContainerDetailIds.Contains(1).Should().Be(true);

            var nugetComponent = result.DetectedComponents.Single(x => x.Component is NuGetComponent);
            nugetComponent.TopLevelReferrers.Count().Should().Be(1);
            (nugetComponent.TopLevelReferrers.First() as PipComponent).Name.Should().Be("sample-root");
            nugetComponent.IsDevelopmentDependency.Should().Be(null);
        }

        [TestMethod]
        public void DetectComponents_DetectOnlyWithIdAndCategoryRestrictions()
        {
            var args = new BcdeArguments
            {
                DetectorCategories = new[] { "Category1", "Category2" },
                DetectorsFilter = new[] { "Detector1", "Detector2" },
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var componentRecorder = new ComponentRecorder();
            var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("/location");
            singleFileComponentRecorder.RegisterUsage(detectedComponents[0]);
            singleFileComponentRecorder.RegisterUsage(detectedComponents[1]);

            var result = DetectComponentsHappyPath(args, restrictions =>
            {
                restrictions.AllowedDetectorCategories.Should().Contain(args.DetectorCategories);
                restrictions.AllowedDetectorIds.Should().Contain(args.DetectorsFilter);
            }, new List<ComponentRecorder> { componentRecorder });

            result.Result.Should().Be(ProcessingResultCode.Success);
            ValidateDetectedComponents(result.DetectedComponents);
        }

        [TestMethod]
        public void DetectComponents_DetectOnlyWithNoUrl()
        {
            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var componentRecorder = new ComponentRecorder();
            var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("/location");
            singleFileComponentRecorder.RegisterUsage(detectedComponents[0]);
            singleFileComponentRecorder.RegisterUsage(detectedComponents[1]);

            var result = DetectComponentsHappyPath(args, restrictions =>
            {
            }, new List<ComponentRecorder> { componentRecorder });

            result.Result.Should().Be(ProcessingResultCode.Success);
            ValidateDetectedComponents(result.DetectedComponents);
        }

        [TestMethod]
        public void DetectComponents_ReturnsExperimentalDetectorInformation()
        {
            componentDetector2Mock.As<IExperimentalDetector>();
            componentDetector3Mock.As<IExperimentalDetector>();

            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var componentRecorder = new ComponentRecorder();
            var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("/location");
            singleFileComponentRecorder.RegisterUsage(detectedComponents[0]);
            singleFileComponentRecorder.RegisterUsage(detectedComponents[1]);

            var result = DetectComponentsHappyPath(args, restrictions => { }, new List<ComponentRecorder> { componentRecorder });

            result.Result.Should().Be(ProcessingResultCode.Success);
            ValidateDetectedComponents(result.DetectedComponents);
            result.DetectorsInRun.All(x => x.IsExperimental).Should().BeTrue();
        }

        [TestMethod]
        public void DetectComponents_Graph_Happy_Path()
        {
            string mockGraphLocation = "/some/dependency/graph";

            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var componentRecorder = new ComponentRecorder();
            var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder(mockGraphLocation);
            singleFileComponentRecorder.RegisterUsage(detectedComponents[0], isExplicitReferencedDependency: true, isDevelopmentDependency: true);
            singleFileComponentRecorder.RegisterUsage(detectedComponents[1], isDevelopmentDependency: false, parentComponentId: detectedComponents[0].Component.Id);

            Mock<IDependencyGraph> mockDependencyGraphA = new Mock<IDependencyGraph>();
            mockDependencyGraphA.Setup(x => x.GetComponents()).Returns(new[] { detectedComponents[0].Component.Id, detectedComponents[1].Component.Id });
            mockDependencyGraphA.Setup(x => x.GetDependenciesForComponent(detectedComponents[0].Component.Id))
                .Returns(new[] { detectedComponents[1].Component.Id });
            mockDependencyGraphA.Setup(x => x.IsComponentExplicitlyReferenced(detectedComponents[0].Component.Id)).Returns(true);
            mockDependencyGraphA.Setup(x => x.IsDevelopmentDependency(detectedComponents[0].Component.Id)).Returns(true);
            mockDependencyGraphA.Setup(x => x.IsDevelopmentDependency(detectedComponents[1].Component.Id)).Returns(false);

            var result = DetectComponentsHappyPath(args, restrictions => { }, new List<ComponentRecorder> { componentRecorder });

            result.Result.Should().Be(ProcessingResultCode.Success);
            result.DependencyGraphs.Count().Should().Be(1);
            var matchingGraph = result.DependencyGraphs.First();
            matchingGraph.Key.Should().Be(mockGraphLocation);
            var explicitlyReferencedComponents = matchingGraph.Value.ExplicitlyReferencedComponentIds;
            explicitlyReferencedComponents.Count.Should().Be(1);
            explicitlyReferencedComponents.Should().Contain(detectedComponents[0].Component.Id);

            var actualGraph = matchingGraph.Value.Graph;
            actualGraph.Keys.Count.Should().Be(2);
            actualGraph[detectedComponents[0].Component.Id].Count().Should().Be(1);
            actualGraph[detectedComponents[0].Component.Id].Should().Contain(detectedComponents[1].Component.Id);
            actualGraph[detectedComponents[1].Component.Id].Should().BeNull();

            matchingGraph.Value.DevelopmentDependencies.Should().Contain(detectedComponents[0].Component.Id);
            matchingGraph.Value.DevelopmentDependencies.Should().NotContain(detectedComponents[1].Component.Id);

            matchingGraph.Value.Dependencies.Should().Contain(detectedComponents[1].Component.Id);
            matchingGraph.Value.Dependencies.Should().NotContain(detectedComponents[0].Component.Id);
        }

        [TestMethod]
        public void DetectComponents_Graph_AccumulatesGraphsOnSameLocation()
        {
            string mockGraphLocation = "/some/dependency/graph";

            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var componentRecorder = new ComponentRecorder();

            Mock<IDependencyGraph> mockDependencyGraphA = new Mock<IDependencyGraph>();
            mockDependencyGraphA.Setup(x => x.GetComponents()).Returns(new[] { detectedComponents[0].Component.Id, detectedComponents[1].Component.Id });
            mockDependencyGraphA.Setup(x => x.GetDependenciesForComponent(detectedComponents[0].Component.Id))
                .Returns(new[] { detectedComponents[1].Component.Id });
            mockDependencyGraphA.Setup(x => x.IsComponentExplicitlyReferenced(detectedComponents[0].Component.Id)).Returns(true);

            var singleFileComponentRecorderA = componentRecorder.CreateSingleFileComponentRecorder(mockGraphLocation);
            singleFileComponentRecorderA.RegisterUsage(detectedComponents[0], isExplicitReferencedDependency: true);
            singleFileComponentRecorderA.RegisterUsage(detectedComponents[1], parentComponentId: detectedComponents[0].Component.Id);

            Mock<IDependencyGraph> mockDependencyGraphB = new Mock<IDependencyGraph>();
            mockDependencyGraphB.Setup(x => x.GetComponents()).Returns(new[] { detectedComponents[0].Component.Id, detectedComponents[1].Component.Id });
            mockDependencyGraphB.Setup(x => x.GetDependenciesForComponent(detectedComponents[1].Component.Id))
                .Returns(new[] { detectedComponents[0].Component.Id });
            mockDependencyGraphB.Setup(x => x.IsComponentExplicitlyReferenced(detectedComponents[1].Component.Id)).Returns(true);

            var singleFileComponentRecorderB = componentRecorder.CreateSingleFileComponentRecorder(mockGraphLocation);
            singleFileComponentRecorderB.RegisterUsage(detectedComponents[1], isExplicitReferencedDependency: true);
            singleFileComponentRecorderB.RegisterUsage(detectedComponents[0], parentComponentId: detectedComponents[1].Component.Id);

            var result = DetectComponentsHappyPath(args, restrictions => { }, new List<ComponentRecorder> { componentRecorder });

            result.Result.Should().Be(ProcessingResultCode.Success);
            result.DependencyGraphs.Count().Should().Be(1);
            var matchingGraph = result.DependencyGraphs.First();
            matchingGraph.Key.Should().Be(mockGraphLocation);
            var explicitlyReferencedComponents = matchingGraph.Value.ExplicitlyReferencedComponentIds;
            explicitlyReferencedComponents.Count.Should().Be(2);
            explicitlyReferencedComponents.Should().Contain(detectedComponents[0].Component.Id);
            explicitlyReferencedComponents.Should().Contain(detectedComponents[1].Component.Id);

            var actualGraph = matchingGraph.Value.Graph;
            actualGraph.Keys.Count.Should().Be(2);
            actualGraph[detectedComponents[0].Component.Id].Count().Should().Be(1);
            actualGraph[detectedComponents[0].Component.Id].Should().Contain(detectedComponents[1].Component.Id);
            actualGraph[detectedComponents[1].Component.Id].Count().Should().Be(1);
            actualGraph[detectedComponents[1].Component.Id].Should().Contain(detectedComponents[0].Component.Id);
        }

        [TestMethod]
        public void VerifyTranslation_ComponentsAreReturnedWithDevDependencyInfo()
        {
            var componentRecorder = new ComponentRecorder();
            var npmDetector = new NpmComponentDetectorWithRoots();
            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location");
            var detectedComponent1 = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector);
            var detectedComponent2 = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector);
            var detectedComponent3 = new DetectedComponent(new NpmComponent("test", "3.0.0"), detector: npmDetector);

            singleFileComponentRecorder.RegisterUsage(detectedComponent1, isDevelopmentDependency: true);
            singleFileComponentRecorder.RegisterUsage(detectedComponent2, isDevelopmentDependency: false);
            singleFileComponentRecorder.RegisterUsage(detectedComponent3);

            var results = SetupRecorderBasedScanning(args, new List<ComponentRecorder> { componentRecorder });

            var detectedComponents = results.ComponentsFound;

            var storedComponent1 = detectedComponents.First(dc => dc.Component.Id == detectedComponent1.Component.Id);
            storedComponent1.IsDevelopmentDependency.Should().BeTrue();

            var storedComponent2 = detectedComponents.First(dc => dc.Component.Id == detectedComponent2.Component.Id);
            storedComponent2.IsDevelopmentDependency.Should().BeFalse();

            var storedComponent3 = detectedComponents.First(dc => dc.Component.Id == detectedComponent3.Component.Id);
            storedComponent3.IsDevelopmentDependency.Should().BeNull();
        }

        [TestMethod]
        public void VerifyTranslation_RootsFromMultipleLocationsAreAgregated()
        {
            var componentRecorder = new ComponentRecorder();
            var npmDetector = new NpmComponentDetectorWithRoots();
            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location1");
            var detectedComponent1 = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector);
            var detectedComponent2 = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector);

            singleFileComponentRecorder.RegisterUsage(detectedComponent1, isExplicitReferencedDependency: true);
            singleFileComponentRecorder.RegisterUsage(detectedComponent2, parentComponentId: detectedComponent1.Component.Id);

            singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location2");
            var detectedComponent2NewLocation = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector);
            singleFileComponentRecorder.RegisterUsage(detectedComponent2NewLocation, isExplicitReferencedDependency: true);

            var results = SetupRecorderBasedScanning(args, new List<ComponentRecorder> { componentRecorder });

            var detectedComponents = results.ComponentsFound;

            var storedComponent1 = detectedComponents.First(dc => dc.Component.Id == detectedComponent1.Component.Id);
            storedComponent1.TopLevelReferrers.Should().HaveCount(1);
            storedComponent1.TopLevelReferrers.Should().Contain(detectedComponent1.Component);

            var storedComponent2 = detectedComponents.First(dc => dc.Component.Id == detectedComponent2.Component.Id);
            storedComponent2.TopLevelReferrers.Should().HaveCount(2, "There 2 roots the component is root of itself in one location and other location the root is its parent");
            storedComponent2.TopLevelReferrers.Should().Contain(detectedComponent1.Component);
            storedComponent2.TopLevelReferrers.Should().Contain(detectedComponent2.Component);
        }

        [TestMethod]
        public void VerifyTranslation_ComponentsAreReturnedWithRoots()
        {
            var componentRecorder = new ComponentRecorder();
            var npmDetector = new NpmComponentDetectorWithRoots();
            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location");
            var detectedComponent1 = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector);
            var detectedComponent2 = new DetectedComponent(new NpmComponent("test", "2.0.0"), detector: npmDetector);

            singleFileComponentRecorder.RegisterUsage(detectedComponent1, isExplicitReferencedDependency: true);
            singleFileComponentRecorder.RegisterUsage(detectedComponent2, parentComponentId: detectedComponent1.Component.Id);

            var results = SetupRecorderBasedScanning(args, new List<ComponentRecorder> { componentRecorder });

            var detectedComponents = results.ComponentsFound;

            var storedComponent1 = detectedComponents.First(dc => dc.Component.Id == detectedComponent1.Component.Id);
            storedComponent1.TopLevelReferrers.Should().HaveCount(1, "If a component is a root, then is root of itself");
            storedComponent1.TopLevelReferrers.Should().Contain(detectedComponent1.Component);

            var storedComponent2 = detectedComponents.First(dc => dc.Component.Id == detectedComponent2.Component.Id);
            storedComponent2.TopLevelReferrers.Should().HaveCount(1);
            storedComponent2.TopLevelReferrers.Should().Contain(detectedComponent1.Component);
        }

        [TestMethod]
        public void VerifyTranslation_DevDependenciesAreMergedWhenSameComponentInDifferentFiles()
        {
            var componentRecorder = new ComponentRecorder();
            var npmDetector = new NpmComponentDetectorWithRoots();
            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var firstRecorder = componentRecorder.CreateSingleFileComponentRecorder("FileA");
            var secondRecorder = componentRecorder.CreateSingleFileComponentRecorder("FileB");

            // These two merged should be true.
            var componentAWithNoDevDep = new DetectedComponent(new NpmComponent("testA", "1.0.0"), detector: npmDetector);
            var componentAWithDevDepTrue = new DetectedComponent(new NpmComponent("testA", "1.0.0"), detector: npmDetector);

            // These two merged should be false.
            var componentBWithNoDevDep = new DetectedComponent(new NpmComponent("testB", "1.0.0"), detector: npmDetector);
            var componentBWithDevDepFalse = new DetectedComponent(new NpmComponent("testB", "1.0.0"), detector: npmDetector);

            // These two merged should be false.
            var componentCWithDevDepTrue = new DetectedComponent(new NpmComponent("testC", "1.0.0"), detector: npmDetector);
            var componentCWithDevDepFalse = new DetectedComponent(new NpmComponent("testC", "1.0.0"), detector: npmDetector);

            // These two merged should be true.
            var componentDWithDevDepTrue = new DetectedComponent(new NpmComponent("testD", "1.0.0"), detector: npmDetector);
            var componentDWithDevDepTrueCopy = new DetectedComponent(new NpmComponent("testD", "1.0.0"), detector: npmDetector);

            // The hint for reading this test is to know that each "column" you see visually is what's being merged, so componentAWithNoDevDep is being merged "down" into componentAWithDevDepTrue.
#pragma warning disable format
            foreach ((DetectedComponent component, bool? isDevDep) component in new[] 
            {
                (componentAWithNoDevDep, (bool?)null), (componentAWithDevDepTrue, true),
                (componentBWithNoDevDep, (bool?)null), (componentBWithDevDepFalse, false),
                (componentCWithDevDepTrue, true), (componentCWithDevDepFalse, false),
                (componentDWithDevDepTrue, true), (componentDWithDevDepTrueCopy, true),
            })
#pragma warning restore format
            {
                firstRecorder.RegisterUsage(component.component, isDevelopmentDependency: component.isDevDep);
            }

            var results = SetupRecorderBasedScanning(args, new List<ComponentRecorder> { componentRecorder });

            var components = results.ComponentsFound;

            components.Single(x => ((NpmComponent)x.Component).Name == "testA").IsDevelopmentDependency.Should().Be(true);
            components.Single(x => ((NpmComponent)x.Component).Name == "testB").IsDevelopmentDependency.Should().Be(false);
            components.Single(x => ((NpmComponent)x.Component).Name == "testC").IsDevelopmentDependency.Should().Be(false);
            components.Single(x => ((NpmComponent)x.Component).Name == "testD").IsDevelopmentDependency.Should().Be(true);
        }

        [TestMethod]
        public void VerifyTranslation_LocationsAreMergedWhenSameComponentInDifferentFiles()
        {
            var componentRecorder = new ComponentRecorder();
            var npmDetector = new NpmComponentDetectorWithRoots();
            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var firstRecorder = componentRecorder.CreateSingleFileComponentRecorder(Path.Join(sourceDirectory.FullName, "/some/file/path"));
            firstRecorder.AddAdditionalRelatedFile(Path.Join(sourceDirectory.FullName, "/some/related/file/1"));
            var secondRecorder = componentRecorder.CreateSingleFileComponentRecorder(Path.Join(sourceDirectory.FullName, "/some/other/file/path"));
            secondRecorder.AddAdditionalRelatedFile(Path.Join(sourceDirectory.FullName, "/some/related/file/2"));

            var firstComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector);
            var secondComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector);

            firstRecorder.RegisterUsage(firstComponent);
            secondRecorder.RegisterUsage(secondComponent);

            var results = SetupRecorderBasedScanning(args, new List<ComponentRecorder> { componentRecorder });

            var actualComponent = results.ComponentsFound.Single();

            actualComponent.LocationsFoundAt.Count().Should().Be(4);
            foreach (var path in new[]
                                        {
                                            "/some/file/path",
                                            "/some/other/file/path",
                                            "/some/related/file/1",
                                            "/some/related/file/2",
                                        })
            {
                actualComponent.LocationsFoundAt
                    .FirstOrDefault(x => x == path)
                    .Should().NotBeNull();
            }
        }

        [TestMethod]
        public void VerifyTranslation_RootsAreMergedWhenSameComponentInDifferentFiles()
        {
            var componentRecorder = new ComponentRecorder();
            var npmDetector = new NpmComponentDetectorWithRoots();
            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var firstRecorder = componentRecorder.CreateSingleFileComponentRecorder("FileA");
            var secondRecorder = componentRecorder.CreateSingleFileComponentRecorder("FileB");

            var root1 = new DetectedComponent(new NpmComponent("test1", "2.0.0"), detector: npmDetector);
            var firstComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector);

            var root2 = new DetectedComponent(new NpmComponent("test2", "3.0.0"), detector: npmDetector);
            var secondComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector);

            firstRecorder.RegisterUsage(root1, isExplicitReferencedDependency: true);
            firstRecorder.RegisterUsage(firstComponent, parentComponentId: root1.Component.Id);

            secondRecorder.RegisterUsage(root2, isExplicitReferencedDependency: true);
            secondRecorder.RegisterUsage(secondComponent, parentComponentId: root2.Component.Id);

            var results = SetupRecorderBasedScanning(args, new List<ComponentRecorder> { componentRecorder });

            var actualComponent = results.ComponentsFound.First(c => c.Component.Id == firstComponent.Component.Id);
            actualComponent.TopLevelReferrers.Count().Should().Be(2);
            actualComponent.TopLevelReferrers.OfType<NpmComponent>()
                .FirstOrDefault(x => x.Name == "test1" && x.Version == "2.0.0")
                .Should().NotBeNull();
            actualComponent.TopLevelReferrers.OfType<NpmComponent>()
                .FirstOrDefault(x => x.Name == "test2" && x.Version == "3.0.0")
                .Should().NotBeNull();
        }

        [TestMethod]
        public void VerifyTranslation_DetectedComponentExist_UpdateFunctionIsApplied()
        {
            var componentRecorder = new ComponentRecorder();
            var npmDetector = new NpmComponentDetectorWithRoots();
            var args = new BcdeArguments
            {
                AdditionalPluginDirectories = Enumerable.Empty<DirectoryInfo>(),
                SourceDirectory = sourceDirectory,
            };

            var singleFileComponentRecorder = componentRecorder.CreateSingleFileComponentRecorder("location");
            var detectedComponent = new DetectedComponent(new NpmComponent("test", "1.0.0"), detector: npmDetector);

            singleFileComponentRecorder.RegisterUsage(detectedComponent, isDevelopmentDependency: true);

            var results = SetupRecorderBasedScanning(args, new List<ComponentRecorder> { componentRecorder });
            results.ComponentsFound.Where(component => component.Component.Id == detectedComponent.Component.Id).Single().IsDevelopmentDependency.Should().BeTrue();

            singleFileComponentRecorder.RegisterUsage(detectedComponent, isDevelopmentDependency: false);

            results = SetupRecorderBasedScanning(args, new List<ComponentRecorder> { componentRecorder });
            results.ComponentsFound.Where(component => component.Component.Id == detectedComponent.Component.Id).Single().IsDevelopmentDependency.Should().BeFalse();
        }

        private TestOutput DetectComponentsHappyPath(
            BcdeArguments args,
            Action<DetectorRestrictions> restrictionAsserter = null,
            IEnumerable<ComponentRecorder> componentRecorders = null)
        {
            var registeredDetectors = new[] { componentDetector2Mock.Object, componentDetector3Mock.Object, versionedComponentDetector1Mock.Object };
            var restrictedDetectors = new[] { componentDetector2Mock.Object, componentDetector3Mock.Object };
            detectorRegistryServiceMock.Setup(x => x.GetDetectors(Enumerable.Empty<DirectoryInfo>(), It.IsAny<IEnumerable<string>>()))
                .Returns(registeredDetectors);
            detectorRestrictionServiceMock.Setup(
                x => x.ApplyRestrictions(
                    It.IsAny<DetectorRestrictions>(),
                    It.Is<IEnumerable<IComponentDetector>>(inputDetectors => registeredDetectors.Intersect(inputDetectors).Count() == registeredDetectors.Count())))
                .Returns(restrictedDetectors)
                .Callback<DetectorRestrictions, IEnumerable<IComponentDetector>>(
                    (restrictions, detectors) => restrictionAsserter?.Invoke(restrictions));

            // We initialize detected component's DetectedBy here because of a Moq constraint -- certain operations (Adding interfaces) have to happen before .Object
            detectedComponents[0].DetectedBy = componentDetector2Mock.Object;
            detectedComponents[1].DetectedBy = componentDetector3Mock.Object;

            var processingResult = new DetectorProcessingResult
            {
                ResultCode = ProcessingResultCode.Success,
                ContainersDetailsMap = new Dictionary<int, ContainerDetails>
                {
                    { sampleContainerDetails.Id, sampleContainerDetails },
                },
                ComponentRecorders = componentRecorders.Select(componentRecorder => (componentDetector2Mock.Object, componentRecorder)),
            };

            detectorProcessingServiceMock.Setup(x =>
                x.ProcessDetectorsAsync(
                   args,
                   It.Is<IEnumerable<IComponentDetector>>(inputDetectors => restrictedDetectors.Intersect(inputDetectors).Count() == restrictedDetectors.Count()),
                   Match.Create<DetectorRestrictions>(restriction => true)))
                .ReturnsAsync(processingResult);

            var result = serviceUnderTest.ExecuteScanAsync(args).Result;
            result.ResultCode.Should().Be(ProcessingResultCode.Success);

            var testOutput = new TestOutput((DefaultGraphScanResult)result);

            return testOutput;
        }

        private ScanResult SetupRecorderBasedScanning(
            BcdeArguments args,
            IEnumerable<ComponentRecorder> componentRecorders)
        {
            var registeredDetectors = new[] { componentDetector2Mock.Object, componentDetector3Mock.Object, versionedComponentDetector1Mock.Object };
            var restrictedDetectors = new[] { componentDetector2Mock.Object, componentDetector3Mock.Object };
            detectorRegistryServiceMock.Setup(x => x.GetDetectors(Enumerable.Empty<DirectoryInfo>(), It.IsAny<IEnumerable<string>>()))
                .Returns(registeredDetectors);
            detectorRestrictionServiceMock.Setup(
                x => x.ApplyRestrictions(
                    It.IsAny<DetectorRestrictions>(),
                    It.Is<IEnumerable<IComponentDetector>>(inputDetectors => registeredDetectors.Intersect(inputDetectors).Count() == registeredDetectors.Count())))
                .Returns(restrictedDetectors);

            // We initialize detected component's DetectedBy here because of a Moq constraint -- certain operations (Adding interfaces) have to happen before .Object
            detectedComponents[0].DetectedBy = componentDetector2Mock.Object;
            detectedComponents[1].DetectedBy = componentDetector3Mock.Object;

            var processingResult = new DetectorProcessingResult
            {
                ResultCode = ProcessingResultCode.Success,
                ContainersDetailsMap = new Dictionary<int, ContainerDetails>
                {
                    { sampleContainerDetails.Id, sampleContainerDetails },
                },
                ComponentRecorders = componentRecorders.Select(componentRecorder => (componentDetector2Mock.Object, componentRecorder)),
            };

            detectorProcessingServiceMock.Setup(x =>
                x.ProcessDetectorsAsync(
                   args,
                   It.Is<IEnumerable<IComponentDetector>>(inputDetectors => restrictedDetectors.Intersect(inputDetectors).Count() == restrictedDetectors.Count()),
                   Match.Create<DetectorRestrictions>(restriction => true)))
                .ReturnsAsync(processingResult);

            var result = serviceUnderTest.ExecuteScanAsync(args).Result;
            result.ResultCode.Should().Be(ProcessingResultCode.Success);

            return result;
        }

        private void ValidateDetectedComponents(IEnumerable<ScannedComponent> scannedComponents)
        {
            var npmComponent = scannedComponents.Where(x => x.Component.Type == ComponentType.Npm).Select(x => x.Component as NpmComponent).FirstOrDefault();
            npmComponent.Should().NotBeNull();
            npmComponent.Name.Should().Be(((NpmComponent)detectedComponents[0].Component).Name);
            var nugetComponent = scannedComponents.Where(x => x.Component.Type == ComponentType.NuGet).Select(x => x.Component as NuGetComponent).FirstOrDefault();
            nugetComponent.Should().NotBeNull();
            nugetComponent.Name.Should().Be(((NuGetComponent)detectedComponents[1].Component).Name);
        }

        private class TestOutput
        {
            public TestOutput(DefaultGraphScanResult result)
            {
                Result = result.ResultCode;
                DetectedComponents = result.ComponentsFound;
                DetectorsInRun = result.DetectorsInScan;
                DependencyGraphs = result.DependencyGraphs;
            }

            internal ProcessingResultCode Result { get; set; }

            internal IEnumerable<ScannedComponent> DetectedComponents { get; set; }

            internal IEnumerable<Detector> DetectorsInRun { get; set; }

            internal DependencyGraphCollection DependencyGraphs { get; set; }
        }
    }
}
