using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Where the newest Onyx setup job stands, in the words the setup step and the needs-you row both use (setup-once spec §5.6).</summary>
/// <param name="Phase">None, working, waiting for the login, or failed.</param>
/// <param name="Line">The job's latest progress line while it works, or null.</param>
/// <param name="FailureCode">The error code of a failure, or null.</param>
/// <param name="FailureSentence">The failure's plain sentence, or null.</param>
public sealed record OnyxSetupJobFact(OnyxJobPhase Phase, string? Line = null, string? FailureCode = null, string? FailureSentence = null)
{
    private static readonly string[] _returnsToTodo = [SindriErrors.SetupOnyxNotWaiting.Code, SindriErrors.JobInterrupted.Code];

    /// <summary>The fact when no job counts.</summary>
    public static OnyxSetupJobFact None { get; } = new(OnyxJobPhase.None);

    /// <summary>The fact of the newest setup job: working or waiting shows as it is; a failure shows unless the owner's wait ran out or a restart cut it (those go back to todo, as the spec says).</summary>
    /// <param name="newest">The newest 'SetupOnyx' job's row, or null.</param>
    /// <returns>The fact.</returns>
    public static OnyxSetupJobFact From(JobRow? newest) => newest switch
    {
        { State: JobState.Running } => new OnyxSetupJobFact(OnyxJobPhase.Working, newest.Progress.MaxBy(static l => l.Round)?.Line),
        { State: JobState.Waiting } => new OnyxSetupJobFact(OnyxJobPhase.WaitingForLogin),
        { State: JobState.Failed, Code: { } code } when !_returnsToTodo.Contains(code) => new OnyxSetupJobFact(OnyxJobPhase.Failed, Line: null, code, newest.Summary ?? "Setting up Onyx didn't finish."),
        _ => None,
    };
}
