using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Not running page's start lines (debug; #3630): which attempt ran, ended and why, so a stuck Starting can be read from the log.</summary>
internal static partial class SindriStartLog
{
    /// <summary>A start was launched.</summary>
    /// <param name="logger">The app's log.</param>
    /// <param name="attempt">The attempt's number.</param>
    [LoggerMessage(EventId = 60, EventName = "StartBegan", Level = LogLevel.Debug, Message = "start: started attempt={Attempt}")]
    internal static partial void Began(ILogger logger, int attempt);

    /// <summary>The 30 s wait ended.</summary>
    /// <param name="logger">The app's log.</param>
    /// <param name="attempt">The attempt the wait belonged to.</param>
    /// <param name="current">The current attempt.</param>
    /// <param name="stage">The stage the page was in.</param>
    [LoggerMessage(EventId = 61, EventName = "StartWaitEnded", Level = LogLevel.Debug, Message = "start: wait ended attempt={Attempt} current={Current} stage={Stage}")]
    internal static partial void WaitEnded(ILogger logger, int attempt, int current, StartStage stage);

    /// <summary>The started program ended.</summary>
    /// <param name="logger">The app's log.</param>
    /// <param name="launch">The launch that ended.</param>
    /// <param name="exitCode">Its exit code.</param>
    /// <param name="attempt">The current attempt.</param>
    /// <param name="stage">The stage the page was in.</param>
    [LoggerMessage(EventId = 62, EventName = "StartProgramExited", Level = LogLevel.Debug, Message = "start: program exited launch={Launch} code={ExitCode} attempt={Attempt} stage={Stage}")]
    internal static partial void ProgramExited(ILogger logger, int launch, int exitCode, int attempt, StartStage stage);
}
