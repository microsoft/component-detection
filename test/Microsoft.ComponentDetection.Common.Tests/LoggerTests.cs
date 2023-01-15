namespace Microsoft.ComponentDetection.Common.Tests;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class LoggerTests
{
    private readonly Mock<IFileWritingService> fileWritingServiceMock;
    private readonly Mock<IConsoleWritingService> consoleWritingServiceMock;

    public LoggerTests()
    {
        this.consoleWritingServiceMock = new Mock<IConsoleWritingService>();
        this.fileWritingServiceMock = new Mock<IFileWritingService>();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        this.consoleWritingServiceMock.VerifyAll();
        this.fileWritingServiceMock.VerifyAll();
    }

    [TestMethod]
    public void LogCreateLoggingGroup_HandlesFailedInit()
    {
        var logger = new Logger(this.consoleWritingServiceMock.Object, null);

        // This should throw an exception while setting up the file writing service, but handle it
        logger.Init(VerbosityMode.Normal);

        this.consoleWritingServiceMock.Invocations.Clear();
        this.consoleWritingServiceMock.Setup(x => x.Write(Environment.NewLine));

        // This should not fail, despite not initializing the file writing service
        logger.LogCreateLoggingGroup();

        // As a result of handling the file writing service failure, the verbosity should now be Verbose
        var verboseMessage = "verboseMessage";
        var expectedMessage = $"[VERBOSE] {verboseMessage} {Environment.NewLine}";
        this.consoleWritingServiceMock.Setup(x => x.Write(expectedMessage));

        logger.LogVerbose(verboseMessage);
    }

    [TestMethod]
    public void LogCreateLoggingGroup_WritesOnNormal()
    {
        var logger = this.CreateLogger(VerbosityMode.Normal);
        this.consoleWritingServiceMock.Setup(x => x.Write(Environment.NewLine));
        this.fileWritingServiceMock.Setup(x => x.AppendToFile(Logger.LogRelativePath, Environment.NewLine));
        logger.LogCreateLoggingGroup();
    }

    [TestMethod]
    public void LogCreateLoggingGroup_SkipsConsoleOnQuiet()
    {
        var logger = this.CreateLogger(VerbosityMode.Quiet);
        this.fileWritingServiceMock.Setup(x => x.AppendToFile(Logger.LogRelativePath, Environment.NewLine));
        logger.LogCreateLoggingGroup();
    }

    [TestMethod]
    public void LogWarning_WritesOnNormal()
    {
        var logger = this.CreateLogger(VerbosityMode.Normal);
        var warningMessage = "warningMessage";
        var expectedMessage = $"[WARN] {warningMessage} {Environment.NewLine}";
        this.consoleWritingServiceMock.Setup(x => x.Write(expectedMessage));
        this.fileWritingServiceMock.Setup(x => x.AppendToFile(Logger.LogRelativePath, expectedMessage));
        logger.LogWarning(warningMessage);
    }

    [TestMethod]
    public void LogWarning_SkipsConsoleOnQuiet()
    {
        var logger = this.CreateLogger(VerbosityMode.Quiet);
        var warningMessage = "warningMessage";
        var expectedMessage = $"[WARN] {warningMessage} {Environment.NewLine}";
        this.fileWritingServiceMock.Setup(x => x.AppendToFile(Logger.LogRelativePath, expectedMessage));
        logger.LogWarning(warningMessage);
    }

    [TestMethod]
    public void LogInfo_WritesOnNormal()
    {
        var logger = this.CreateLogger(VerbosityMode.Normal);
        var infoMessage = "informationalMessage";
        var expectedMessage = $"[INFO] {infoMessage} {Environment.NewLine}";
        this.consoleWritingServiceMock.Setup(x => x.Write(expectedMessage));
        this.fileWritingServiceMock.Setup(x => x.AppendToFile(Logger.LogRelativePath, expectedMessage));
        logger.LogInfo(infoMessage);
    }

    [TestMethod]
    public void LogInfo_SkipsConsoleOnQuiet()
    {
        var logger = this.CreateLogger(VerbosityMode.Quiet);
        var infoMessage = "informationalMessage";
        var expectedMessage = $"[INFO] {infoMessage} {Environment.NewLine}";
        this.fileWritingServiceMock.Setup(x => x.AppendToFile(Logger.LogRelativePath, expectedMessage));
        logger.LogInfo(infoMessage);
    }

    [TestMethod]
    public void LogVerbose_WritesOnVerbose()
    {
        var logger = this.CreateLogger(VerbosityMode.Verbose);
        var verboseMessage = "verboseMessage";
        var expectedMessage = $"[VERBOSE] {verboseMessage} {Environment.NewLine}";
        this.consoleWritingServiceMock.Setup(x => x.Write(expectedMessage));
        this.fileWritingServiceMock.Setup(x => x.AppendToFile(Logger.LogRelativePath, expectedMessage));
        logger.LogVerbose(verboseMessage);
    }

    [TestMethod]
    public void LogVerbose_SkipsConsoleOnNormal()
    {
        var logger = this.CreateLogger(VerbosityMode.Normal);
        var verboseMessage = "verboseMessage";
        var expectedMessage = $"[VERBOSE] {verboseMessage} {Environment.NewLine}";
        this.fileWritingServiceMock.Setup(x => x.AppendToFile(Logger.LogRelativePath, expectedMessage));
        logger.LogVerbose(verboseMessage);
    }

    [TestMethod]
    public void LogError_WritesOnQuiet()
    {
        var logger = this.CreateLogger(VerbosityMode.Quiet);
        var errorMessage = "errorMessage";
        var expectedMessage = $"[ERROR] {errorMessage} {Environment.NewLine}";
        this.consoleWritingServiceMock.Setup(x => x.Write(expectedMessage));
        this.fileWritingServiceMock.Setup(x => x.AppendToFile(Logger.LogRelativePath, expectedMessage));
        logger.LogError(errorMessage);
    }

    [TestMethod]
    public void LogFailedReadingFile_WritesOnVerbose()
    {
        var logger = this.CreateLogger(VerbosityMode.Verbose);
        var filePath = "some/bad/file/path";
        var error = new UnauthorizedAccessException("Some unauthorized access error");

        var consoleSequence = new MockSequence();
        this.consoleWritingServiceMock.InSequence(consoleSequence).Setup(x => x.Write(Environment.NewLine));
        this.consoleWritingServiceMock.InSequence(consoleSequence).Setup(x => x.Write(
            Match.Create<string>(message => message.StartsWith("[VERBOSE]") && message.Contains(filePath))));
        this.consoleWritingServiceMock.InSequence(consoleSequence).Setup(x => x.Write(
            Match.Create<string>(message => message.StartsWith("[INFO]") && message.Contains(error.Message))));

        var fileSequence = new MockSequence();
        this.fileWritingServiceMock.InSequence(fileSequence).Setup(x => x.AppendToFile(
            Logger.LogRelativePath,
            Match.Create<string>(message => message.StartsWith("[VERBOSE]") && message.Contains(filePath))));
        this.fileWritingServiceMock.InSequence(fileSequence).Setup(x => x.AppendToFile(
            Logger.LogRelativePath,
            Match.Create<string>(message => message.StartsWith("[INFO]") && message.Contains(error.Message))));

        logger.LogFailedReadingFile(filePath, error);
    }

    [TestMethod]
    public void LogFailedReadingFile_SkipsConsoleOnQuiet()
    {
        var logger = this.CreateLogger(VerbosityMode.Quiet);
        var filePath = "some/bad/file/path";
        var error = new UnauthorizedAccessException("Some unauthorized access error");

        var fileSequence = new MockSequence();
        this.fileWritingServiceMock.InSequence(fileSequence).Setup(x => x.AppendToFile(
            Logger.LogRelativePath,
            Match.Create<string>(message => message.StartsWith("[VERBOSE]") && message.Contains(filePath))));
        this.fileWritingServiceMock.InSequence(fileSequence).Setup(x => x.AppendToFile(
            Logger.LogRelativePath,
            Match.Create<string>(message => message.StartsWith("[INFO]") && message.Contains(error.Message))));

        logger.LogFailedReadingFile(filePath, error);
    }

    [TestMethod]
    public void LogException_WritesOnQuietIfError()
    {
        var logger = this.CreateLogger(VerbosityMode.Quiet);
        var error = new UnauthorizedAccessException("Some unauthorized access error");

        this.consoleWritingServiceMock.Setup(x => x.Write(
            Match.Create<string>(message => message.StartsWith("[ERROR]") && message.Contains(error.Message))));

        this.fileWritingServiceMock.Setup(x => x.AppendToFile(
            Logger.LogRelativePath,
            Match.Create<string>(message => message.StartsWith("[ERROR]") && message.Contains(error.ToString()))));

        logger.LogException(error, true);
    }

    [TestMethod]
    public void LogException_DoesNotLogFullExceptionByDefault()
    {
        var logger = this.CreateLogger(VerbosityMode.Quiet);
        var error = new UnauthorizedAccessException("Some unauthorized access error");

        this.consoleWritingServiceMock.Setup(x => x.Write(
            Match.Create<string>(message => message.StartsWith("[ERROR]") && message.Contains(error.Message) && !message.Contains(error.ToString()))));

        this.fileWritingServiceMock.Setup(x => x.AppendToFile(
            Logger.LogRelativePath,
            Match.Create<string>(message => message.StartsWith("[ERROR]") && message.Contains(error.ToString()))));

        logger.LogException(error, true);
    }

    [TestMethod]
    public void LogException_LogsFullExceptionOnRequest()
    {
        var logger = this.CreateLogger(VerbosityMode.Quiet);
        var error = new UnauthorizedAccessException("Some unauthorized access error");

        this.consoleWritingServiceMock.Setup(x => x.Write(
            Match.Create<string>(message => message.StartsWith("[ERROR]") && message.Contains(error.ToString()))));

        this.fileWritingServiceMock.Setup(x => x.AppendToFile(
            Logger.LogRelativePath,
            Match.Create<string>(message => message.StartsWith("[ERROR]") && message.Contains(error.ToString()))));

        logger.LogException(error, true, printException: true);
    }

    [TestMethod]
    public void LogException_SkipsConsoleIfNotErrorAndNormalLogging()
    {
        var logger = this.CreateLogger(VerbosityMode.Normal);
        var error = new UnauthorizedAccessException("Some unauthorized access error");

        this.fileWritingServiceMock.Setup(x => x.AppendToFile(
            Logger.LogRelativePath,
            Match.Create<string>(message => message.StartsWith("[INFO]") && message.Contains(error.ToString()))));

        logger.LogException(error, false);
    }

    [TestMethod]
    public void LogException_WritesEverythingIfNotErrorAndVerboseLogging()
    {
        var logger = this.CreateLogger(VerbosityMode.Verbose);
        var error = new UnauthorizedAccessException("Some unauthorized access error");

        this.consoleWritingServiceMock.Setup(x => x.Write(
            Match.Create<string>(message => message.StartsWith("[INFO]") && message.Contains(error.Message))));

        this.fileWritingServiceMock.Setup(x => x.AppendToFile(
            Logger.LogRelativePath,
            Match.Create<string>(message => message.StartsWith("[INFO]") && message.Contains(error.Message))));

        logger.LogException(error, false);
    }

    private Logger CreateLogger(VerbosityMode verbosityMode)
    {
        var serviceUnderTest = new Logger(this.consoleWritingServiceMock.Object, this.fileWritingServiceMock.Object);

        serviceUnderTest.Init(verbosityMode);

        // We're not explicitly testing init behavior here, so we reset mock expecations. Another test should verify these.
        this.consoleWritingServiceMock.Invocations.Clear();
        this.fileWritingServiceMock.Invocations.Clear();
        return serviceUnderTest;
    }
}
