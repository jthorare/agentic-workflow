using Aw.Kernel;

namespace Aw.Sindri.Core.Lanes;

/// <summary>
/// Reads the lane team's state from 'aw-meta', read-only (Home spec §3.3): each member returns the JSON the command printed, so the Home collector parses it.
/// Nothing here may run a command that writes (merge-run, gate-run, lane-recycle).
/// </summary>
public interface ILaneSource
{
    /// <summary>Runs 'aw-meta status --json'.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The printed JSON, or SND-LANES-001 when aw-meta is missing, times out, exits 2 or more, or prints nothing.</returns>
    public ValueTask<Result<string>> Status(CancellationToken cancellationToken);

    /// <summary>Runs 'aw-meta lanes-context --json' (exit 1 means a lane needs attention and still prints valid JSON).</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The printed JSON, or SND-LANES-001.</returns>
    public ValueTask<Result<string>> Context(CancellationToken cancellationToken);

    /// <summary>Runs 'aw-meta usage --json' (exit 1 means a subject is limited and still prints valid JSON).</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The printed JSON, or SND-LANES-001.</returns>
    public ValueTask<Result<string>> Usage(CancellationToken cancellationToken);
}
