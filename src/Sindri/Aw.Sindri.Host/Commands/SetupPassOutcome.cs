namespace Aw.Sindri.Host.Commands;

/// <summary>How the setup pass that 'setup --apply' ran ended.</summary>
/// <param name="State">'done', 'timeout', 'failed', 'skipped' (--timeout 0: no wait) or 'refused' (a caller that may not run services: the pass runs on its next tick, within 60 s).</param>
/// <param name="JobId">The pass's job, or null when none started or the pass in flight is a scheduled one (SND-JOB-409 naming no job).</param>
/// <param name="Seconds">How long the wait took, in whole seconds.</param>
internal sealed record SetupPassOutcome(string State, string? JobId, int Seconds)
{
    /// <summary>The pass ended well.</summary>
    internal const string Done = "done";

    /// <summary>The wait ended first; the pass keeps running.</summary>
    internal const string Timeout = "timeout";

    /// <summary>The pass failed, or couldn't start.</summary>
    internal const string Failed = "failed";

    /// <summary>--timeout 0: the pass started and nothing waited for it.</summary>
    internal const string Skipped = "skipped";

    /// <summary>This caller may not run services; the scheduler runs the pass on its next tick.</summary>
    internal const string Refused = "refused";

    /// <summary>The first line of the pass's error when it failed, for SND-SETUP-016; null otherwise. Not in the document.</summary>
    internal string? Error { get; init; }

    /// <summary>True for an end that makes 'setup --apply' exit 1.</summary>
    internal bool Bad => string.Equals(State, Timeout, StringComparison.Ordinal) || string.Equals(State, Failed, StringComparison.Ordinal);
}
