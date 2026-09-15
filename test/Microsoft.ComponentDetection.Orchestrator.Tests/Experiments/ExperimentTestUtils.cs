#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Experiments;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

public static class ExperimentTestUtils
{
    public static ScannedComponent CreateRandomScannedComponent() => new ScannedComponent() { Component = CreateRandomTypedComponent() };

    public static TypedComponent CreateRandomTypedComponent() => new NpmComponent(Guid.NewGuid().ToString(), CreateRandomVersion());

    public static (string DetectorId, TimeSpan DetectorRunTime) CreateRandomDetector() => (Guid.NewGuid().ToString(), TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(0, 1000)));

    public static List<ScannedComponent> CreateRandomComponents(int length = 5) =>
        Enumerable.Range(0, length).Select(_ => CreateRandomScannedComponent()).ToList();

    public static List<ExperimentComponent> CreateRandomExperimentComponents(int length = 5) =>
        CreateRandomComponents(length).Select(x => new ExperimentComponent(x)).ToList();

    public static List<(string DetectorId, TimeSpan DetectorRunTime)> CreateRandomDetectorList(int length = 1) =>
        Enumerable.Range(0, length).Select(_ => CreateRandomDetector()).ToList();

    private static string CreateRandomVersion() =>
        $"{RandomNumberGenerator.GetInt32(0, 100)}.{RandomNumberGenerator.GetInt32(0, 100)}.{RandomNumberGenerator.GetInt32(0, 100)}";
}
