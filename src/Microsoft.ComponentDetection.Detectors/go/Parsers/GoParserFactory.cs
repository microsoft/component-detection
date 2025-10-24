#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Go;

using System;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

public class GoParserFactory : IGoParserFactory
{
    private readonly IFileUtilityService fileUtilityService;
    private readonly ICommandLineInvocationService commandLineInvocationService;

    public GoParserFactory(IFileUtilityService fileUtilityService, ICommandLineInvocationService commandLineInvocationService)
    {
        this.fileUtilityService = fileUtilityService;
        this.commandLineInvocationService = commandLineInvocationService;
    }

    public IGoParser CreateParser(GoParserType type, ILogger logger)
    {
        return type switch
        {
            GoParserType.GoMod => new GoModParser(logger),
            GoParserType.GoSum => new GoSumParser(logger),
            GoParserType.GoCLI => new GoCLIParser(logger, this.fileUtilityService, this.commandLineInvocationService),
            _ => throw new ArgumentException($"Unknown parser type: {type}"),
        };
    }
}
