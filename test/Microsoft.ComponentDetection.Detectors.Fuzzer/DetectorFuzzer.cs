namespace Microsoft.ComponentDetection.Detectors.Fuzzer;

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Common.DependencyGraph;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.Extensions.DependencyInjection;

public static class DetectorFuzzer
{
    public static void FuzzDetectors(ReadOnlySpan<byte> input)
    {
        var serviceProvider = new ServiceCollection().AddComponentDetection().BuildServiceProvider();

        var detectors = serviceProvider.GetServices<IComponentDetector>();

        foreach (var detector in detectors)
        {
            if (detector is FileComponentDetector fileDetector)
            {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                fileDetector.OnFileFoundAsync(
                    new ProcessRequest()
                    {
                        ComponentStream = new FuzzComponentStream(input),
                        SingleFileComponentRecorder = new ComponentRecorder().CreateSingleFileComponentRecorder(Path.GetTempFileName()),
                    },
                    new Dictionary<string, string>())
                            .GetAwaiter()
                            .GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

            }
        }
    }

    public class FuzzComponentStream : IComponentStream
    {
        public FuzzComponentStream(ReadOnlySpan<byte> input) =>

            // todo: create a steam without copying the span
            this.Stream = new MemoryStream(input.ToArray());

        public Stream Stream { get; set; }

        public string Pattern => "*";

        public string Location => Path.GetRandomFileName();
    }
}
