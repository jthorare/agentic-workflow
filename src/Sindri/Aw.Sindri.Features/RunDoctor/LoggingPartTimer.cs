using Aw.Sindri.Core.Doctor;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Writes each part's time as a debug line, the same shape as the per-class lines in <see cref="WithMoreChecks"/> (#3390).</summary>
/// <param name="log">The doctor's logger.</param>
internal sealed partial class LoggingPartTimer(ILogger<LoggingPartTimer> log) : IPartTimer
{
    /// <inheritdoc/>
    public void Took(string part, long elapsedMilliseconds) => LogPart(log, part, elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Debug, Message = "doctor.check {Check} {ElapsedMs} ms")]
    private static partial void LogPart(ILogger logger, string check, long elapsedMs);
}
