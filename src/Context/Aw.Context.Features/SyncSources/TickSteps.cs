using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sync;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// The steps the index tick adds to a feed beyond the decision pass: <see cref="LinearViewerRun"/> for the Linear projects feed, and <see cref="LinearLookupsRun"/> for the Linear issue feed. A host that registers no viewer, identity store,
/// lookups or lookup store gets none of them. A feed with more than one step runs them in order on the same row.
/// </summary>
/// <param name="clock">The clock.</param>
/// <param name="viewers">The viewer of each source that has one; null: none.</param>
/// <param name="identities">The identity store; null: none.</param>
/// <param name="lookups">Reads the pickers' lists from Linear; null: none.</param>
/// <param name="lookupStore">Keeps the pickers' lists; null: none.</param>
internal sealed class TickSteps(TimeProvider clock, ISourceViewers? viewers = null, ISourceIdentityStore? identities = null, ILinearLookups? lookups = null, ILinearLookupStore? lookupStore = null)
{
    /// <summary>The step for <paramref name="feed"/>, or null for none. With several, one that runs them in order.</summary>
    /// <param name="feed">The feed that ticked.</param>
    /// <param name="settings">The checked settings (the steps exist only when Linear is configured).</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>The step, or null.</returns>
    public Func<SourceSync, ValueTask<SourceSync>>? For(RecordFeed feed, ContextSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(settings);
        var steps = Steps(feed, settings, cancellationToken);
        return steps.Count switch
        {
            0 => null,
            1 => steps[0],
            _ => Chain(steps),
        };
    }

    private static Func<SourceSync, ValueTask<SourceSync>> Chain(List<Func<SourceSync, ValueTask<SourceSync>>> steps) => async row =>
    {
        foreach (var step in steps)
        {
            row = await step(row).ConfigureAwait(false);
        }

        return row;
    };

    private List<Func<SourceSync, ValueTask<SourceSync>>> Steps(RecordFeed feed, ContextSettings settings, CancellationToken cancellationToken)
    {
        List<Func<SourceSync, ValueTask<SourceSync>>> steps = [];
        if (identities is not null && feed.Source == LinearSourceIds.Projects && viewers?.ForLinear(settings) is { } viewer)
        {
            var run = new LinearViewerRun(viewer, identities, clock);
            steps.Add(row => run.Execute(row, cancellationToken));
        }

        if (lookups is not null && lookupStore is not null && feed.Source == LinearSourceIds.Issues && settings.Linear is { } linear)
        {
            var run = new LinearLookupsRun(lookups, lookupStore, linear, clock);
            steps.Add(row => run.Execute(row, cancellationToken));
        }

        return steps;
    }
}
