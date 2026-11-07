using Aw.Kernel;

namespace Aw.Sindri.Core.Linear;

/// <summary>
/// What the owner's stored Linear data says for the lookups and the strip (Linear hub spec 2026-11-04 §4.2, §4.3), in Sindri's own records: Context's IContextIssues behind an adapter.
/// Reads stored data only; the caller's scope applies.
/// </summary>
public interface ILinearSideFacts
{
    /// <summary>Reads a team's pickers.</summary>
    /// <param name="team">The team's key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lists; a picked team never synced answers null ids, name and sync time and empty lists. Null when Context holds nothing for the team in this scope (it isn't a picked team). Context's error otherwise (SND-CONTEXT-020 for a session).</returns>
    public ValueTask<Result<LinearLookupsResponse?>> Lookups(string team, CancellationToken cancellationToken);

    /// <summary>Reads whether the profile names any Linear team (<c language="text">context.sources.linear.teams</c>).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when it does; the error when the profile can't be read.</returns>
    public ValueTask<Result<bool>> TeamsPicked(CancellationToken cancellationToken);

    /// <summary>Reads what changed for the owner after an instant.</summary>
    /// <param name="after">Only changes after this instant count.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The changes, oldest first; null when the owner's Linear user isn't known yet. Context's error otherwise (SND-CONTEXT-020 for a session).</returns>
    public ValueTask<Result<IReadOnlyList<LinearSinceItem>?>> Since(DateTimeOffset after, CancellationToken cancellationToken);
}
