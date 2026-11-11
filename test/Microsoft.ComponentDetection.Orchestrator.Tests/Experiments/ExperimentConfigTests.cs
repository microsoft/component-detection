#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Composer;
using Microsoft.ComponentDetection.Detectors.CondaLock;
using Microsoft.ComponentDetection.Detectors.Swift;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Configs;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class ExperimentConfigTests
{
    private List<IExperimentConfiguration> configs;

    [TestInitialize]
    public void Initialize()
    {
        var serviceProvider = new ServiceCollection().AddComponentDetection().BuildServiceProvider();

        this.configs = serviceProvider.GetServices<IExperimentConfiguration>().ToList();
    }

    [TestMethod]
    public void ExperimentConfig_HaveDistinctNames()
    {
        var names = this.configs.Select(c => c.Name).ToList();

        names.Should().OnlyHaveUniqueItems();
    }

    [TestMethod]
    public void ExperimentConfig_SwiftDetectorIsNotInExperimentGroup()
    {
        var swiftDetector = new SwiftResolvedComponentDetector(null, null, null);

        this.configs.Should().OnlyContain(config => !config.IsInExperimentGroup(swiftDetector));
    }

    [TestMethod]
    public void ExperimentConfig_CondaLockDetectorIsNotInExperimentGroup()
    {
        var condaLockDetector = new CondaLockComponentDetector(null, null, null);

        this.configs.Should().OnlyContain(config => !config.IsInExperimentGroup(condaLockDetector));
    }

    [TestMethod]
    public void ExperimentConfig_ComposerDetectorIsOnlyInComposerExperiment()
    {
        var detector = new ComposerComponentDetector(null, null, null, null, null, null);
        var config = this.configs.Should().ContainSingle(config => config.IsInExperimentGroup(detector)).Which;

        config.Should().BeOfType<ComposerExperiment>().Which.Name.Should().Be("Composer");
        this.configs.Should().OnlyContain(config => !config.IsInControlGroup(detector));
    }

    [TestMethod]
    public void ExperimentConfig_ComposerExperimentExcludesOtherDetectors()
    {
        var config = this.configs.OfType<ComposerExperiment>().Single();
        var detector = Mock.Of<IComponentDetector>();

        config.IsInControlGroup(detector).Should().BeFalse();
        config.IsInExperimentGroup(detector).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void ExperimentConfig_ComposerExperimentDoesNotFilterDetectorRuns(int numComponents)
    {
        var config = this.configs.OfType<ComposerExperiment>().Single();
        var detector = new ComposerComponentDetector(null, null, null, null, null, null);

        config.ShouldRecord(detector, numComponents).Should().BeTrue();
        config.ShouldRecord(Mock.Of<IComponentDetector>(), numComponents).Should().BeTrue();
    }

    [TestMethod]
    public async Task ExperimentConfig_InitShouldNotThrowAsync()
    {
        foreach (var config in this.configs)
        {
            var init = async () => await config.InitAsync();

            await init.Should().NotThrowAsync($"the experiment {config.Name} threw an exception in InitAsync");
        }
    }

    [TestMethod]
    public void ExperimentConfig_ShouldRecordShouldNotThrow()
    {
        foreach (var config in this.configs)
        {
            var shouldRecord = () => config.ShouldRecord(Mock.Of<IComponentDetector>(), 0);

            shouldRecord.Should().NotThrow($"the experiment {config.Name} threw an exception in ShouldRecordAsync");
        }
    }

    [TestMethod]
    public void ExperimentConfig_IsInControlGroupShouldNotThrow()
    {
        foreach (var config in this.configs)
        {
            var isInControlGroup = () => config.IsInControlGroup(Mock.Of<IComponentDetector>());

            isInControlGroup.Should().NotThrow($"the experiment {config.Name} threw an exception in IsInControlGroupAsync");
        }
    }

    [TestMethod]
    public void ExperimentConfig_IsInExperimentGroupShouldNotThrow()
    {
        foreach (var config in this.configs)
        {
            var isInExperimentGroup = () => config.IsInExperimentGroup(Mock.Of<IComponentDetector>());

            isInExperimentGroup.Should().NotThrow($"the experiment {config.Name} threw an exception in IsInExperimentGroupAsync");
        }
    }
}
