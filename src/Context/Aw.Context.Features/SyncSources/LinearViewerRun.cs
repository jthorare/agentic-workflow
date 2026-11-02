using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// The step after the Linear projects feed's tick (projects page spec §4.2): who the owner's key belongs to, stored as the 'source_identity' row of 'linear' so the page can tell which
/// projects the owner leads. One viewer call, skipped while the stored row is under an hour old. It only ever adds to the row's outcome: a tick that failed is returned as it is (the call would fail too),
/// and a viewer or store that fails leaves the stored identity and the feed's row as they were; the next tick asks again.
/// </summary>
/// <param name="viewer">Asks Linear who the key belongs to.</param>
/// <param name="identities">The identity store.</param>
/// <param name="clock">The clock the row's age and its read time use.</param>
internal sealed class LinearViewerRun(ISourceViewer viewer, ISourceIdentityStore identities, TimeProvider clock)
{
    /// <summary>How long a stored identity is served before Linear is asked again.</summary>
    internal static readonly TimeSpan Fresh = TimeSpan.FromHours(1);

    /// <summary>Runs the step on the tick's row.</summary>
    /// <param name="row">The feed's row.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>The same row.</returns>
    public async ValueTask<SourceSync> Execute(SourceSync row, CancellationToken cancellationToken)
    {
        if (row.Code is not null)
        {
            return row;
        }

        var now = clock.GetUtcNow();
        var stored = await identities.Read(LinearSourceIds.Identity, cancellationToken).ConfigureAwait(false);
        if (stored.IsError || (stored.Value is { } known && now - known.ReadAt < Fresh))
        {
            return row;
        }

        var asked = await viewer.Viewer(cancellationToken).ConfigureAwait(false);
        if (!asked.IsError)
        {
            _ = await identities.Write(new SourceIdentity(LinearSourceIds.Identity, LinearSourceIds.UserRef(asked.Value.Id), asked.Value.Name, now), cancellationToken).ConfigureAwait(false);
        }

        return row;
    }
}
