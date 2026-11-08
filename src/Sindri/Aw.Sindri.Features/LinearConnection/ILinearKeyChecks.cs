using Aw.Kernel;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// The two questions Settings asks Linear about a key pasted into the page. The key lives in memory for the one call: it is never logged, stored or put in an error.
/// </summary>
internal interface ILinearKeyChecks
{
    /// <summary>Lists the teams the key can read.</summary>
    /// <param name="key">The pasted key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The teams; SND-CONTEXT-071 when Linear refuses the key, SND-CONTEXT-010 when it is unreachable, SND-CONTEXT-072 for a reply of an unknown shape.</returns>
    public ValueTask<Result<IReadOnlyList<LinearConnectionTeamView>>> Teams(string key, CancellationToken cancellationToken);

    /// <summary>Asks what a write key may do. Nothing is written to Linear.</summary>
    /// <param name="key">The pasted key.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The scope; SND-CONTEXT-071 when Linear refuses the key, SND-CONTEXT-010 when it is unreachable.</returns>
    public ValueTask<Result<LinearKeyScope>> WriteScope(string key, CancellationToken cancellationToken);
}
