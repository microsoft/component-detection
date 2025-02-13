namespace Microsoft.ComponentDetection.Detectors.Go;

using System;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

public class GoParserFactory : IGoParserFactory
{
    private readonly ILogger logger;
    private readonly IFileUtilityService fileUtilityService;
    private readonly ICommandLineInvocationService commandLineInvocationService;

    public GoParserFactory(ILogger logger, IFileUtilityService fileUtilityService, ICommandLineInvocationService commandLineInvocationService)
    {
        this.logger = logger;
        this.fileUtilityService = fileUtilityService;
        this.commandLineInvocationService = commandLineInvocationService;
    }

    public IGoParser CreateParser(GoParserType type)
    {
        return type switch
        {
            GoParserType.GoMod => new GoModParser(this.logger),
            GoParserType.GoSum => new GoSumParser(this.logger),
            GoParserType.GoCLI => new GoCLIParser(this.logger, this.fileUtilityService, this.commandLineInvocationService),
            _ => throw new ArgumentException($"Unknown parser type: {type}"),
        };
    }
}
