using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sync;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// The steps the index tick adds to a feed beyond the decision pass: <see cref="LinearViewerRun"/> for the Linear projects feed only. A host that registers no viewer or identity store gets none.
/// </summary>
/// <param name="clock">The clock.</param>
/// <param name="viewers">The viewer of each source that has one; null: none.</param>
/// <param name="identities">The identity store; null: none.</param>
internal sealed class TickSteps(TimeProvider clock, ISourceViewers? viewers = null, ISourceIdentityStore? identities = null)
{
    /// <summary>The step for <paramref name="feed"/>, or null for none.</summary>
    /// <param name="feed">The feed that ticked.</param>
    /// <param name="settings">The checked settings (the viewer exists only when Linear is configured).</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>The step, or null.</returns>
    public Func<SourceSync, ValueTask<SourceSync>>? For(RecordFeed feed, ContextSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(settings);
        if (identities is null || feed.Source != LinearSourceIds.Projects || viewers?.ForLinear(settings) is not { } viewer)
        {
            return null;
        }

        var run = new LinearViewerRun(viewer, identities, clock);
        return row => run.Execute(row, cancellationToken);
    }
}
