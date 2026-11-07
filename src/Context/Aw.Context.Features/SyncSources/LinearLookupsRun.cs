using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// The step after the Linear issue feed's tick (linear hub spec 2026-11-04 §4.2): the states, cycles, members and labels of the profile's teams, for the pickers. It runs once per tick, after the issues, and also when the tick
/// ended at its time cap, so the pickers fill during a long re-read. It only reads and replaces each team's rows: a tick that failed is returned as it is (the call would fail too), and a read or write that fails leaves the stored rows
/// and the feed's row as they were; the next tick asks again.
/// </summary>
/// <param name="lookups">Reads the lists from Linear.</param>
/// <param name="store">Keeps them.</param>
/// <param name="settings">The checked 'context.sources.linear' settings.</param>
/// <param name="clock">Stamps each team with the time its lists were replaced.</param>
internal sealed class LinearLookupsRun(ILinearLookups lookups, ILinearLookupStore store, LinearSettings settings, TimeProvider clock)
{
    /// <summary>Runs the step on the tick's row.</summary>
    /// <param name="row">The feed's row.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>The same row.</returns>
    public async ValueTask<SourceSync> Execute(SourceSync row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Code is not null && row.Code != ContextErrors.SyncCapped.Code)
        {
            return row;
        }

        var fetched = await lookups.Fetch(settings, cancellationToken).ConfigureAwait(false);
        if (fetched.IsError)
        {
            return row;
        }

        foreach (var team in fetched.Value)
        {
            _ = await store.ReplaceTeam(team with { SyncedAt = clock.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
        }

        // why: the workspace key is one more read with the same key; a failed or empty one leaves the stored key as it was
        var urlKey = await lookups.FetchWorkspaceUrlKey(settings, cancellationToken).ConfigureAwait(false);
        if (urlKey is { IsSuccess: true, Value: { } key })
        {
            _ = await store.ReplaceWorkspaceUrlKey(key, cancellationToken).ConfigureAwait(false);
        }

        return row;
    }
}
