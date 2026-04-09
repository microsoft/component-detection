namespace Microsoft.ComponentDetection.Detectors.Linux;

using Microsoft.ComponentDetection.Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Factory for creating <see cref="BinarySyftRunner"/> instances.
/// </summary>
internal class BinarySyftRunnerFactory : IBinarySyftRunnerFactory
{
    private readonly ICommandLineInvocationService commandLineInvocationService;
    private readonly ILoggerFactory loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinarySyftRunnerFactory"/> class.
    /// </summary>
    /// <param name="commandLineInvocationService">The command line invocation service.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public BinarySyftRunnerFactory(
        ICommandLineInvocationService commandLineInvocationService,
        ILoggerFactory loggerFactory)
    {
        this.commandLineInvocationService = commandLineInvocationService;
        this.loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public ISyftRunner Create(string binaryPath) =>
        new BinarySyftRunner(
            binaryPath,
            this.commandLineInvocationService,
            this.loggerFactory.CreateLogger<BinarySyftRunner>());
}
