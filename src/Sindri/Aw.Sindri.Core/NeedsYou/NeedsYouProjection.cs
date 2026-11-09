using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>
/// Folds the events that make the human's list into the 'needs_you' view (API spec §3.6, P4 plan Unit C): a filed request is a decision
/// row until it is decided or its item is closed, a failed service run is a failure row until a run succeeds or a profile no
/// longer names the service, and a drifted live profile is a profile row until it reverts or is approved. Each row is stored as a
/// <see cref="NeedsYouRowRecord"/>, keyed by the row's id; the pure rules are <see cref="NeedsYouRows"/> and <see cref="NeedsYouClearing"/>.
/// </summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
/// <param name="scrubber">Scrubs secrets out of the text a row stores.</param>
/// <param name="builtIns">The services the build runs without a profile entry: a failure row of one offers RunService only and outlives a profile check.</param>
internal sealed partial class NeedsYouProjection(EventFactory events, ITextScrubber scrubber, IEnumerable<BuiltInService> builtIns) : IProjection
{
    private IReadOnlyList<BuiltInService> BuiltIns { get; } = [.. builtIns];

    private const string DriftKey = "profile:drift";

    public IReadOnlyList<string> Views => [ViewNames.NeedsYou];

    private static readonly HashSet<string> _handled = new(StringComparer.Ordinal)
    {
        EventTypes.RequestFiled, EventTypes.DecisionRecorded, EventTypes.ItemStateChanged, EventTypes.ServiceRan, EventTypes.ProfileChecked,
        EventTypes.ConnectionNeedsSignIn, EventTypes.ConnectionSignedIn,
    };

    /// <inheritdoc />
    public bool Handles(string eventType) => _handled.Contains(eventType);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return recorded.Envelope.Type switch
        {
            EventTypes.RequestFiled => await Filed(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.DecisionRecorded => await Decided(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.ItemStateChanged => await Closed(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.ServiceRan => await Ran(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.ProfileChecked => await Checked(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.ConnectionNeedsSignIn or EventTypes.ConnectionSignedIn => await ConnectionRow(recorded, rows, cancellationToken).ConfigureAwait(false),
            _ => Results.Success(Unit.Value),
        };
    }

    private async ValueTask<Result<Unit>> Filed(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var filed = events.Payload<RequestFiled>(recorded);
        if (filed.IsError)
        {
            return filed.Error;
        }

        var decision = RequestsProjection.Fold(filed.Value, recorded, scrubber, itemVersion: 0).Decision;
        return await Put(rows, NeedsYouRows.ForDecision(decision, scrubber), recorded, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Decided(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var payload = events.Payload<DecisionRecorded>(recorded);
        return payload.IsError ? payload.Error : await rows.Delete(ViewNames.NeedsYou, "decision:" + payload.Value.Decision.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Closed(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var changed = events.Payload<ItemStateChanged>(recorded);
        if (changed.IsError)
        {
            return changed.Error;
        }

        if (!ItemStates.IsClosed(changed.Value.To))
        {
            return Unit.Value;
        }

        return await ClearDecisions(rows, changed.Value.Item.Value, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<Unit>> ClearDecisions(IViewRows rows, string item, CancellationToken cancellationToken)
    {
        var concerned = await rows.Where<NeedsYouRowRecord>(ViewNames.NeedsYou, ViewColumn.Item, item, cancellationToken).ConfigureAwait(false);
        return concerned.IsError
            ? concerned.Error
            : await DeleteAll(rows, concerned.Value.Where(static r => r.Row.Kind == NeedsYouKind.Decision), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Ran(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var ran = events.Payload<ServiceRan>(recorded);
        if (ran.IsError)
        {
            return ran.Error;
        }

        var key = "failure:service:" + ran.Value.Service.Value;
        var existing = await rows.Get<NeedsYouRowRecord>(ViewNames.NeedsYou, key, cancellationToken).ConfigureAwait(false);
        if (existing.IsError)
        {
            return existing.Error;
        }

        var profile = await rows.Get<ProfileRow>(ViewNames.Profile, ProfileProjection.Key, cancellationToken).ConfigureAwait(false);
        if (profile.IsError)
        {
            return profile.Error;
        }

        var named = profile.Value?.ApprovedServices.Contains(ran.Value.Service) == true;
        var service = ServicesProjection.Fold(row: null, ran.Value, recorded);
        var next = NeedsYouClearing.AfterRun(existing.Value?.Row, service, scrubber, BuiltInService.Is(ran.Value.Service, named, BuiltIns));
        return await Replace(rows, key, next, recorded, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Checked(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var check = events.Payload<ProfileChecked>(recorded);
        if (check.IsError)
        {
            return check.Error;
        }

        var profile = ProfileProjection.Fold(row: null, check.Value, recorded);
        var drift = await rows.Delete(ViewNames.NeedsYou, DriftKey, cancellationToken).ConfigureAwait(false);   // #2258 PR 1: no drift row any more; drops one an older build stored
        if (drift.IsError)
        {
            return drift;
        }

        var failures = await rows.Where<NeedsYouRowRecord>(ViewNames.NeedsYou, ViewColumn.Kind, "failure", cancellationToken).ConfigureAwait(false);
        return failures.IsError
            ? failures.Error
            : await DeleteAll(rows, failures.Value.Where(r => !NeedsYouClearing.FailureStands(r.Row, profile, [.. BuiltIns.Select(static b => b.Name)])), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<Unit>> Put(IViewRows rows, Result<NeedsYouRow> row, LedgerEvent recorded, CancellationToken cancellationToken) =>
        row.IsError
            ? row.Error
            : await rows.Put(ViewNames.NeedsYou, row.Value.Id.Value, new NeedsYouRowRecord(row.Value, recorded.Sequence), cancellationToken).ConfigureAwait(false);

    private static async ValueTask<Result<Unit>> Replace(IViewRows rows, string key, Result<NeedsYouRow?> next, LedgerEvent recorded, CancellationToken cancellationToken)
    {
        if (next.IsError)
        {
            return next.Error;
        }

        return next.Value is { } row
            ? await rows.Put(ViewNames.NeedsYou, key, new NeedsYouRowRecord(row, recorded.Sequence), cancellationToken).ConfigureAwait(false)
            : await rows.Delete(ViewNames.NeedsYou, key, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<Unit>> DeleteAll(IViewRows rows, IEnumerable<NeedsYouRowRecord> doomed, CancellationToken cancellationToken)
    {
        foreach (var record in doomed)
        {
            var deleted = await rows.Delete(ViewNames.NeedsYou, record.Row.Id.Value, cancellationToken).ConfigureAwait(false);
            if (deleted.IsError)
            {
                return deleted;
            }
        }

        return Unit.Value;
    }
}
