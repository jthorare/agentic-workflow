using System.Globalization;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The ledger entries of the map's build, promotion and refresh, one place for the jobs and the promote command to append them.</summary>
/// <param name="Ledger">Appends the entries.</param>
/// <param name="Events">Builds the entries.</param>
/// <param name="Grant">The lease the entries are fenced by.</param>
internal readonly record struct ContextMapEvents(ILedgerStore Ledger, EventFactory Events, LeaseGrant Grant)
{
    /// <summary>Appends 'context.map.built' for a build that began at <paramref name="slot"/>.</summary>
    internal async ValueTask<Result<ContextMapBuilt>> Built(MapBuildReport report, DateTimeOffset slot, CancellationToken cancellationToken)
    {
        var payload = new ContextMapBuilt(report.Draft.Repo.Value, report.Draft.Number, report.Proposed.Count, report.Degraded);
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapBuilt}:{Grant.Epoch}:{slot:O}");
        var appended = await Ledger.Append(Events.Entry(EventTypes.ContextMapBuilt, payload, key), Grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : payload;
    }

    /// <summary>Appends 'context.map.promoted' for a promotion.</summary>
    internal async ValueTask<Result<LedgerAppend>> Promoted(MapPromoted promoted, CancellationToken cancellationToken)
    {
        var version = promoted.Version;
        var payload = new ContextMapPromoted(version.Number, version.Repo.Value, promoted.Retained?.Number);
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapPromoted}:{version.Repo.Value}:v{version.Number}");
        return await Ledger.Append(Events.Entry(EventTypes.ContextMapPromoted, payload, key), Grant, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends 'context.map.refreshed' for a finished refresh.</summary>
    internal async ValueTask<Result<LedgerAppend>> Refreshed(RefreshContextMapResult result, CancellationToken cancellationToken)
    {
        var payload = new ContextMapRefreshed(result.Repo, result.Version, result.Heads, result.Accepted, result.Removed, result.Dropped, result.Cleanup, result.Held);
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapRefreshed}:{result.Repo}:v{result.Version}");
        return await Ledger.Append(Events.Entry(EventTypes.ContextMapRefreshed, payload, key), Grant, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends 'context.map.held' for a draft held for the code index.</summary>
    internal async ValueTask<Result<LedgerAppend>> HeldFor(ContextMapHeld payload, CancellationToken cancellationToken)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapHeld}:{payload.Repo}:v{payload.Version}");
        return await Ledger.Append(Events.Entry(EventTypes.ContextMapHeld, payload, key), Grant, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends 'context.map.published' for a map that went ahead, after its 'context.map.promoted'.</summary>
    internal async ValueTask<Result<LedgerAppend>> Published(ContextMapPublished payload, CancellationToken cancellationToken)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapPublished}:{payload.Repo}:v{payload.Version}");
        return await Ledger.Append(Events.Entry(EventTypes.ContextMapPublished, payload, key), Grant, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends 'context.map.hold.dropped' for a hold that ended with no promote; <paramref name="attempt"/> keeps one drop apart from another of the same version.</summary>
    internal async ValueTask<Result<LedgerAppend>> HoldDropped(string repo, int version, string reason, string attempt, CancellationToken cancellationToken)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapHoldDropped}:{repo}:v{version}:{attempt}");
        return await Ledger.Append(Events.Entry(EventTypes.ContextMapHoldDropped, new ContextMapHoldDropped(version, reason), key), Grant, cancellationToken).ConfigureAwait(false);
    }
}
