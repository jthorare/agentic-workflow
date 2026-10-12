using Microsoft.Extensions.Logging;

namespace Aw.Judge.Host.Commands;

/// <summary>The commands' log lines (JSON lines on stderr through Aw.Kernel.Telemetry; AW_LOG_LEVEL filters them).</summary>
internal static partial class JudgeLog
{
    /// <summary>The logger category of every judge command.</summary>
    internal const string Category = "Aw.Judge";

    /// <summary>The stored config is invalid, so the command uses the defaults.</summary>
    [LoggerMessage(EventId = 1, EventName = "ConfigDefaults", Level = LogLevel.Warning, Message = "Using the default config: {Reason}")]
    internal static partial void UsingDefaultConfig(ILogger logger, string reason);
}
