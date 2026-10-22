using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Outbox;
using Aw.Redaction;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Effects are activities (engine spec rev 8 §5.7; engine P6). After a transition's ledger event the interpreter, for
/// each effect: writes its 'outbox.pending' row ('RecordEffect', a local activity keyed like 'RecordEvent'), waits
/// 'workflow.dispatchDelay' on a workflow timer (cancelled with the instance), rechecks the recipe's approval and the
/// profile's allowlist ('RecheckEffect', a local activity), then either runs it ('RunEffect', a regular activity that
/// calls the block's 'IEffectKind', writes 'outbox.dispatched' and appends the reply event caused by that row) or
/// writes 'outbox.refused' and hands the instance an 'OutboxRefused' trigger. Kernel's 'outbox_commands' is not used.
/// <para>
/// 'RunEffect' keys what it writes by the effect identity, not by its activity: the dispatched row
/// 'workflow:{address}:effect:{identity}:dispatched' and the reply '…:reply' (under the instance's address, so 'workflow events' lists them), each with an envelope id derived from its key.
/// An activity retry or a re-emission (a new command id, the same identity) therefore writes nothing twice, and its reply
/// still names the first dispatched row as its causation.
/// </para>
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds; every append is fenced by it.</param>
/// <param name="events">The control plane's event factory.</param>
/// <param name="catalog">The registered events: a block's reply outcome names the reply event that declares it.</param>
/// <param name="dispatch">Runs an effect block's runtime, by its id.</param>
/// <param name="recipes">The recipe approvals now (P2's IRecipeApprovals and PinRules.Check), for the recheck.</param>
/// <param name="profile">The approved profile snapshot (never the live file), whose 'workflow.effects' the recheck reads.</param>
internal sealed class EffectActivities(
    ILedgerStore ledger, HeldLease lease, EventFactory events, BlockCatalog catalog, EffectDispatch dispatch, IApprovedRecipes recipes, IApprovedProfile profile)
{
    /// <summary>'RecordEffect' and 'RecheckEffect' run as local activities that retry without bound (the ledger must have the row).</summary>
    public static LocalActivityOptions LocalOptions => RecordEventActivities.LocalOptions;

    /// <summary>No allowlist: an engine-origin recheck reads no profile, and the engine's desk notice needs none.</summary>
    private static readonly EffectSettings _noEffects = new(Allowlist: null, EffectSettings.DefaultDispatchDelay);

    /// <summary>
    /// 'RunEffect' runs as a regular activity: retried with the outbox policy's backoff (Kernel's <see cref="OutboxRetryPolicy"/>,
    /// 8 attempts, spec A-F20-F25), since the block's existence check and the identity-keyed rows make a repeat harmless;
    /// a block that answers with an error is retried. Once the attempts are spent the effect is refused and the instance parks.
    /// </summary>
    public static ActivityOptions RunOptions { get; } = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(1),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = OutboxRetryPolicy.Default.BaseDelay,
            BackoffCoefficient = 2,
            MaximumInterval = OutboxRetryPolicy.Default.MaxDelay,
            MaximumAttempts = OutboxRetryPolicy.Default.MaxAttempts,
        },
    };

    /// <summary>Appends <paramref name="record"/>'s row under 'workflow:{workflow id}:{run id}:{activity id}'.</summary>
    /// <param name="record">The row.</param>
    /// <returns>Its ledger position.</returns>
    [Activity("RecordEffect")]
    public async Task<LedgerAppend> RecordAsync(EffectRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var activity = ActivityExecutionContext.Current;
        var entry = events.Entry(OutboxEvents.Of(record.Row.State), record.Row, RecordEventActivities.KeyOf(activity.Info), record.Item);
        return await Append(entry, activity.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The recheck after the dispatch delay (§5.7, §4.3 W11): the recipe's approval and the approved profile's
    /// 'workflow.effects', read now. An engine-origin effect reads neither (only its kind is checked). A read that fails
    /// throws, so the local activity retries it.
    /// </summary>
    /// <param name="recheck">The recipe, the effect and its origin.</param>
    /// <returns>Null, or the refusal's code and message.</returns>
    [Activity("RecheckEffect")]
    public async Task<EffectRecheckResult> RecheckAsync(EffectRecheck recheck)
    {
        ArgumentNullException.ThrowIfNull(recheck);
        if (recheck.Origin == CommandOrigin.Engine)
        {
            return Answer(EffectRules.Recheck(recheck.Origin, ApprovalCheck.Holds, _noEffects, recheck.Kind), recheck);
        }

        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var approval = await recipes.Check(recheck.Recipe, cancel).ConfigureAwait(false);
        var approved = await profile.Latest(cancel).ConfigureAwait(false);
        if (approved.IsError || (approval.IsError && approval.Error.Code != WorkflowErrors.RecipeNotApproved.Code))
        {
            var error = approved.IsError ? approved.Error : approval.Error;
            throw new InvalidOperationException($"{error.Code}: {error.Message}");   // retried: the recheck must read both
        }

        // A running instance's recipe with no approval row at all is treated like a revoked one: it no longer holds.
        var check = approval.IsError ? ApprovalCheck.Revoked : approval.Value;
        var settings = approved.Value is { } snapshot ? EffectSettings.Of(snapshot.Profile) : _noEffects;   // unit (d): the S1-D11 keys, checked at approve
        return Answer(EffectRules.Recheck(recheck.Origin, check, settings, recheck.Kind), recheck);
    }

    /// <summary>Performs the effect through its block, writes 'outbox.dispatched' and appends its reply.</summary>
    /// <param name="run">The effect and its emission.</param>
    /// <returns>The dispatched row and the reply.</returns>
    [Activity("RunEffect")]
    public async Task<EffectRunResult> RunAsync(EffectRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var identity = run.Command.Identity.Text;
        await RecheckOnRun(run).ConfigureAwait(false);
        var done = await dispatch(EffectContexts.Of(run), cancel).ConfigureAwait(false);
        if (done.IsError && done.Error.Code == WorkflowErrors.NoRuntime.Code)
        {
            // A setup the effect can't run in (no commit, a repo the profile lacks, no fact:item): retrying can't fix it; the instance parks dispatch.
            throw new ApplicationFailureException(Scrubbed(done.Error.Message), errorType: done.Error.Code, nonRetryable: true);
        }

        if (done.IsError)
        {
            throw new InvalidOperationException(Scrubbed($"{done.Error.Code}: {done.Error.Message}"));   // retried: the block's existence check makes the repeat harmless
        }

        RefuseUndeclaredFacts(run, done.Value);
        var row = new EffectRow(identity, run.CommandId, run.Command.Effect, run.Command.Origin, EffectState.Dispatched, InstanceAddress.From(run.WorkflowId),
            Reason: done.Value.Existed ? "already sent" : null);
        var dispatched = Keyed(events.Entry(OutboxEvents.Dispatched, row, $"workflow:{run.WorkflowId}:effect:{identity}:dispatched", run.Item));
        var at = await Append(dispatched, cancel).ConfigureAwait(false);
        if (done.Value.Reply is { } outcome)
        {
            _ = await Append(Reply(run, outcome, dispatched.Envelope.Id, done.Value.Facts, done.Value.Event), cancel).ConfigureAwait(false);
        }

        return new EffectRunResult(at.Sequence, done.Value.Reply?.Value, done.Value.Existed || at.AlreadyRecorded);
    }

    /// <summary>Each attempt re-checks the approval and allowlist (cheap), so a revoke or drift after the delay, or during the retries, is honoured; a refusal is non-retryable.</summary>
    private async Task RecheckOnRun(EffectRun run)
    {
        if (run.Recipe is not { } recipe)
        {
            return;
        }

        var rechecked = await RecheckAsync(new EffectRecheck(recipe, run.Command.Effect, run.Command.Origin)).ConfigureAwait(false);
        if (rechecked.Code is { } refusal)
        {
            throw new ApplicationFailureException(rechecked.Reason ?? refusal, errorType: refusal, nonRetryable: true);
        }
    }

    /// <summary>
    /// The reply event a block's <paramref name="outcome"/> names (spec §2, §12): the one causation-routed event that
    /// declares it, from that event's producer, caused by the dispatched row so the bridge routes it to this instance only.
    /// </summary>
    private LedgerEntry Reply(EffectRun run, OutcomeName outcome, Guid causation, IReadOnlyDictionary<FactKey, ReplyFact>? facts, EventKind? kind)
    {
        var replies = RepliesTo(outcome, kind);
        if (replies is not [{ } reply])
        {
            throw new ApplicationFailureException(
                $"{run.Command.Effect.Value} replied '{outcome.Value}', which {replies.Count} registered reply events declare (one must)",
                errorType: WorkflowErrors.EffectFailed.Code, nonRetryable: true);
        }

        var entry = Keyed(events.Entry(reply.Kind.Value, new EffectReply(outcome.Value, run.Command.Identity.Text, ReplyFactsOf(facts)), $"workflow:{run.WorkflowId}:effect:{run.Command.Identity.Text}:reply", run.Item));
        return entry with { Envelope = entry.Envelope with { Actor = reply.Producer, CausationId = causation } };
    }

    /// <summary>The reply events that declare <paramref name="outcome"/>; with <paramref name="kind"/> (the effect's own reply event, for an outcome several declare) only that one.</summary>
    private List<EventDescriptor> RepliesTo(OutcomeName outcome, EventKind? kind) =>
        [.. catalog.Events.Where(e => e.Route is EventRoute.Causation or EventRoute.Parent && e.Outcomes.Contains(outcome) && (kind is null || e.Kind == kind))];

    private static Dictionary<string, FactValue>? ReplyFactsOf(IReadOnlyDictionary<FactKey, ReplyFact>? facts) =>
        facts is { Count: > 0 } ? facts.ToDictionary(static f => f.Key.Value, static f => new FactValue(f.Value.Ref, f.Value.Sha256, f.Value.HeadSha), StringComparer.Ordinal) : null;

    /// <summary>
    /// A reply's facts are recorded only through the transition's 'produces' (forge wiring brief item 3), and only the ones the effect's
    /// descriptor lists in 'Outputs' and its reply event declares: anything else is refused here, before any row is written, naming the
    /// facts, so the delivery's declared-facts filter never drops one silently. An instance that began before the 'forge-reply-facts'
    /// marker takes no reply facts.
    /// </summary>
    private void RefuseUndeclaredFacts(EffectRun run, EffectDone done)
    {
        if (done.Facts is not { Count: > 0 } facts)
        {
            return;
        }

        var declared = catalog.Find(run.Command.Effect)?.Outputs.Select(static o => o.Fact).ToHashSet() ?? [];
        var received = done.Reply is { } outcome && RepliesTo(outcome, done.Event) is [{ } reply] ? reply.Facts.ToHashSet() : [];
        var refused = facts.Keys.Where(k => !run.ReplyFacts || !declared.Contains(k) || !received.Contains(k)).Select(static k => k.Value).Order(StringComparer.Ordinal).ToList();
        if (refused.Count > 0)
        {
            var why = run.ReplyFacts ? "the effect's descriptor must list each in Outputs and the reply event declare it" : "this instance began before the forge-reply-facts marker";
            throw new ApplicationFailureException(
                $"{run.Command.Effect.Value} replied with {string.Join(", ", refused)}, which it may not carry: {why}",
                errorType: WorkflowErrors.EffectFailed.Code, nonRetryable: true);
        }
    }

    /// <summary><paramref name="entry"/> with an envelope id derived from its idempotency key, so a repeat names the row already written.</summary>
    private static LedgerEntry Keyed(LedgerEntry entry) =>
        entry with { Envelope = entry.Envelope with { Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(entry.IdempotencyKey)).AsSpan(0, 16)) } };

    private async Task<LedgerAppend> Append(LedgerEntry entry, CancellationToken cancellationToken)
    {
        var grant = lease.Current ?? throw new InvalidOperationException("SND-LEASE-002: this process holds no lease; the effect waits for one");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError
            ? throw new InvalidOperationException($"{appended.Error.Code}: {appended.Error.Message}")
            : appended.Value;
    }

    /// <summary>The reason a block gave, scrubbed like a notice: it may carry a fact's text (a secret-shaped repo name), and it lands in the ledger and the status.</summary>
    private static string Scrubbed(string reason) => Redactor.Default.Scrub(reason).Text;   // a blocked scrub yields its placeholder, never the input

    private static EffectRecheckResult Answer(string? code, EffectRecheck recheck) => new(code, code switch
    {
        null => null,
        _ when code == WorkflowErrors.RecipeRevoked.Code => $"{recheck.Recipe.Id.Value}@{recheck.Recipe.Hash.Display} was revoked within the dispatch delay",
        _ when code == WorkflowErrors.RecipePinDrift.Code => $"a block pinned by {recheck.Recipe.Id.Value}@{recheck.Recipe.Hash.Display} changed since it was approved",
        _ => $"{recheck.Kind.Value} isn't on the approved profile's effect allowlist (workflow.effects)",
    });
}
