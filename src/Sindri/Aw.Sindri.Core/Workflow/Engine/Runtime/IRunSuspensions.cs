using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Whether a launch's run is suspended (sandbox spec §7, SB16): a park, an escalation or a takeover holds it. 'session wrap' reads it on its watchdog tick and counts only
/// unsuspended time against the wall limit. The engine owns the answer; when it is unavailable the caller counts the run as not suspended.
/// </summary>
public interface IRunSuspensions
{
    /// <summary>Whether the run that owns <paramref name="launch"/> is suspended.</summary>
    /// <param name="launch">The launch ('LCH-…').</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True while a park, escalation or takeover holds the run; false for a running run, a closed one and an unknown launch; an error when the answer can't be read.</returns>
    public ValueTask<Result<bool>> IsSuspended(LaunchId launch, CancellationToken cancellationToken);
}
