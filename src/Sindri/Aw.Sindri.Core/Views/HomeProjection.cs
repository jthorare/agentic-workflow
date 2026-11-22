using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>
/// Folds the 'home.*' events into the 'home-rows' and 'home-links' views (home spec §3.1, §8): a row per object, its edges beside it, a pure fold a 'views rebuild' reproduces.
/// An event for a row the view doesn't hold is skipped. The seen, act, brief and cursor events change no row.
/// </summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class HomeProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.HomeRows, ViewNames.HomeLinks];

    public bool Handles(string eventType) => eventType.StartsWith("home.", StringComparison.Ordinal);

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return recorded.Envelope.Type switch
        {
            EventTypes.HomeRowRaised => Raised(recorded, rows, cancellationToken),
            EventTypes.HomeRowChanged => Changed(recorded, rows, cancellationToken),
            EventTypes.HomeRowCleared => Cleared(recorded, rows, cancellationToken),
            _ => States(recorded, rows, cancellationToken),
        };
    }

    // the owner's and the collectors' state moves: each sets the version and the state fields of one row
    private ValueTask<Result<Unit>> States(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken) => recorded.Envelope.Type switch
    {
        EventTypes.HomeRowSnoozed => Edit<HomeRowSnoozed>(recorded, rows, static p => p.Id, static (row, p) => row with { Version = p.Version, State = HomeState.Snoozed, SnoozeUntil = p.Until }, cancellationToken),
        EventTypes.HomeRowWoke => Edit<HomeRowWoke>(recorded, rows, static p => p.Id, static (row, p) => row with { Version = p.Version, State = HomeState.Open, SnoozeUntil = null }, cancellationToken),
        EventTypes.HomeRowDone => Edit<HomeRowDone>(recorded, rows, static p => p.Id, (row, p) => row with { Version = p.Version, State = HomeState.Done, DoneAt = p.At ?? recorded.Envelope.OccurredAt }, cancellationToken),
        EventTypes.HomeRowRevived => Edit<HomeRowRevived>(recorded, rows, static p => p.Id, static (row, p) => row with { Version = p.Version, State = HomeState.Open, DoneAt = null }, cancellationToken),
        EventTypes.HomeRowMuted => Edit<HomeRowMuted>(recorded, rows, static p => p.Id, static (row, p) => row with { Version = p.Version, State = HomeState.Muted }, cancellationToken),
        _ => ValueTask.FromResult<Result<Unit>>(Unit.Value),
    };

    private static string EdgeKey(HomeEdge edge) => $"{edge.From}|{edge.To}|{Enum.GetName(edge.Edge)}";

    private static async ValueTask<Result<Unit>> ReplaceEdges(IViewRows rows, string from, IReadOnlyList<HomeEdge> edges, CancellationToken cancellationToken)
    {
        var cleared = await ClearEdges(rows, from, cancellationToken).ConfigureAwait(false);
        if (cleared.IsError)
        {
            return cleared.Error;
        }

        foreach (var edge in edges)
        {
            var put = await rows.Put(ViewNames.HomeLinks, EdgeKey(edge), edge, cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                return put.Error;
            }
        }

        return Unit.Value;
    }

    private static async ValueTask<Result<Unit>> ClearEdges(IViewRows rows, string from, CancellationToken cancellationToken)
    {
        var held = await rows.Where<HomeEdge>(ViewNames.HomeLinks, ViewColumn.Item, from, cancellationToken).ConfigureAwait(false);
        if (held.IsError)
        {
            return held.Error;
        }

        foreach (var edge in held.Value)
        {
            var deleted = await rows.Delete(ViewNames.HomeLinks, EdgeKey(edge), cancellationToken).ConfigureAwait(false);
            if (deleted.IsError)
            {
                return deleted.Error;
            }
        }

        return Unit.Value;
    }

    private async ValueTask<Result<Unit>> Raised(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var raised = events.Payload<HomeRowRaised>(recorded);
        if (raised.IsError)
        {
            return raised.Error;
        }

        var row = raised.Value.Row;
        var put = await rows.Put(ViewNames.HomeRows, row.Id.Value, row, cancellationToken).ConfigureAwait(false);
        return put.IsError ? put.Error : await ReplaceEdges(rows, row.Id.Value, raised.Value.Edges, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Changed(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var changed = events.Payload<HomeRowChanged>(recorded);
        if (changed.IsError)
        {
            return changed.Error;
        }

        var p = changed.Value;
        var held = await rows.Get<HomeRow>(ViewNames.HomeRows, p.Id.Value, cancellationToken).ConfigureAwait(false);
        if (held.IsError || held.Value is not { } row)
        {
            return held.IsError ? held.Error : Unit.Value;
        }

        var put = await rows.Put(ViewNames.HomeRows, p.Id.Value, Fold(row, p), cancellationToken).ConfigureAwait(false);
        return put.IsError || p.Edges is null ? put : await ReplaceEdges(rows, p.Id.Value, p.Edges, cancellationToken).ConfigureAwait(false);
    }

    // A change names only the fields that moved; the rest stay as the row holds them.
    private static HomeRow Fold(HomeRow row, HomeRowChanged p) => row with
    {
        Version = p.Version,
        Title = p.Title ?? row.Title,
        Facts = p.Facts ?? row.Facts,
        WaitsOn = p.WaitsOn ?? row.WaitsOn,
        Rank = p.Rank ?? row.Rank,
        BlocksList = p.BlocksList ?? row.BlocksList,
        Links = p.Links ?? row.Links,
        Actions = p.Actions ?? row.Actions,
        Source = p.Source ?? row.Source,
    };

    private async ValueTask<Result<Unit>> Cleared(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var cleared = events.Payload<HomeRowCleared>(recorded);
        if (cleared.IsError)
        {
            return cleared.Error;
        }

        var deleted = await rows.Delete(ViewNames.HomeRows, cleared.Value.Id.Value, cancellationToken).ConfigureAwait(false);
        return deleted.IsError ? deleted.Error : await ClearEdges(rows, cleared.Value.Id.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Edit<TPayload>(LedgerEvent recorded, IViewRows rows, Func<TPayload, HomeRowId> id, Func<HomeRow, TPayload, HomeRow> fold, CancellationToken cancellationToken)
    {
        var payload = events.Payload<TPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        var key = id(payload.Value).Value;
        var row = await rows.Get<HomeRow>(ViewNames.HomeRows, key, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is null
            ? Unit.Value
            : await rows.Put(ViewNames.HomeRows, key, fold(row.Value, payload.Value), cancellationToken).ConfigureAwait(false);
    }
}
