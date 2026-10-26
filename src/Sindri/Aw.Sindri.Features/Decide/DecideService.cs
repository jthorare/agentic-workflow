using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Decide;

/// <summary>
/// Decides a decision (API spec §3.2, P4 plan Unit E): reads its row through the views (a session outside its repo gets the same SND-DEC-404 as
/// an unknown id), lets <see cref="DecisionRules"/> settle it, and appends 'decision.recorded' keyed by the decision. Losing the race for that
/// key is SND-ITEM-409 "decided elsewhere"; a retried call is a new command, so it meets that conflict, never a quiet success.
/// </summary>
/// <param name="views">The 'decisions' view.</param>
/// <param name="ledger">Appends the decision.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="scrubber">Takes secrets out of the note before it reaches the ledger.</param>
/// <param name="callers">The caller: the actor and the command id. A session Sindri launched is refused (SND-AUTH-002, q100): a pending decision is the owner's.</param>
internal sealed class DecideService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber, ICallerContextAccessor callers)
    : IService<DecideRequest, CommandAccepted>
{
    public async ValueTask<Result<CommandAccepted>> Handle(DecideRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("Decide needs a caller");
        }

        if (caller.Launch is not null)
        {
            return SindriErrors.AuthRefused.With("Decide is the owner's: a session Sindri launched can't decide a decision. Ask the human, or an agent session the owner started");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var decided = await Settle(request, caller, cancellationToken).ConfigureAwait(false);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var (decision, payload) = decided.Value;
        var recorded = payload with { Note = request.Note is null ? null : SafeText.CleanBlock(scrubber, request.Note, DecisionRules.NoteMax) };
        var entry = events.Entry(EventTypes.DecisionRecorded, recorded, $"{EventTypes.DecisionRecorded}:{decision.Id.Value}", decision.Item?.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        return appended.Value.AlreadyRecorded
            ? SindriErrors.ItemConflict.With($"{decision.Id.Value} was decided elsewhere")
            : new CommandAccepted(caller.CommandId, appended.Value.Sequence);
    }

    /// <summary>The decision as the caller may see it and what deciding it records.</summary>
    private async ValueTask<Result<(HumanDecisionRow Decision, DecisionRecorded Payload)>> Settle(DecideRequest request, CallerContext caller, CancellationToken cancellationToken)
    {
        var row = await views.Get<HumanDecisionRow>(ViewNames.Decisions, request.Decision.Value, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        if (row.Value is not { } decision)
        {
            return SindriErrors.DecisionNotFound.With($"no decision '{request.Decision.Value}'");
        }

        var decided = DecisionRules.Decide(decision, request.Choice, request.Note, caller.Actor, caller.CommandId);
        return decided.IsError ? decided.Error : (decision, decided.Value);
    }
}
