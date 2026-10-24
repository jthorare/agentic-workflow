using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// Appends the registry's events to the ledger chain (engine spec §4.1), under the process's lease grant:
/// 'recipe.registered:&lt;hash&gt;', 'recipe.approved:&lt;hash&gt;:after:&lt;seq&gt;' (so a drift approval of the same hash is its
/// own event) and 'recipe.revoked:&lt;hash&gt;' (one per revoked hash, cascaded ones included). Register, approve and
/// revoke all write through it.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="events">The event factory (the caller's actor and correlation).</param>
/// <param name="held">The grant this process holds.</param>
public sealed class RecipeEvents(ILedgerStore ledger, EventFactory events, HeldLease held)
{
    /// <summary>Appends 'recipe.registered' for <paramref name="recipe"/>; registering the same hash again records nothing.</summary>
    /// <param name="recipe">The stored recipe.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The event's position (AlreadyRecorded on a repeat), or SND-LEDGER-002 when no lease is held.</returns>
    public ValueTask<Result<LedgerAppend>> Registered(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Append(EventTypes.RecipeRegistered, new RecipeRegistered(recipe.Id, recipe.Version, recipe.Hash),
            $"{EventTypes.RecipeRegistered}:{recipe.Hash.Value}", cancellationToken);
    }

    /// <summary>
    /// Appends 'recipe.approved' for <paramref name="approval"/>, keyed by the latest approval event it follows. A retry
    /// whose approval is already the latest event records nothing and returns that event.
    /// </summary>
    /// <param name="approval">The stored approval row.</param>
    /// <param name="drift">True for a drift approval (the same hash re-approved with new pins).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The event's position, or SND-LEDGER-002 when no lease is held.</returns>
    public ValueTask<Result<LedgerAppend>> Approved(RecipeApproval approval, bool drift, CancellationToken cancellationToken) =>
        Approved(approval, drift, batch: null, cancellationToken);

    /// <summary>
    /// Appends 'recipe.approved' for <paramref name="approval"/> as <see cref="Approved(RecipeApproval, bool, CancellationToken)"/> does, carrying <paramref name="batch"/> when one approve-all hold approved it with others.
    /// </summary>
    /// <param name="approval">The stored approval row.</param>
    /// <param name="drift">True for a drift approval (the same hash re-approved with new pins).</param>
    /// <param name="batch">The approve-all hold this approval belongs to, or null.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The event's position, or SND-LEDGER-002 when no lease is held.</returns>
    public async ValueTask<Result<LedgerAppend>> Approved(RecipeApproval approval, bool drift, ApprovalBatch? batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (held.Current is null)
        {
            return NoLease(EventTypes.RecipeApproved);
        }

        var latest = await ledger.Latest(EventTypes.RecipeApproved, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;   // refuse: without the latest approval event the key can't name what it follows
        }

        var payload = Payload(approval, drift, batch);
        if (latest.Value is { } previous && Repeats(previous, payload))
        {
            return new LedgerAppend(previous.Sequence, AlreadyRecorded: true);
        }

        var after = latest.Value?.Sequence ?? 0;
        return await Append(EventTypes.RecipeApproved, payload,
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.RecipeApproved}:{approval.Hash.Value}:after:{after}"), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The ledger sequence of <paramref name="hash"/>'s own standing approval event (the latest 'recipe.approved' whose key starts
    /// 'recipe.approved:&lt;hash&gt;:'), not another recipe's later one: the key of the wake an approval that records nothing sends again.
    /// Approval events chain through their ':after:&lt;seq&gt;' keys, so the walk is one read per approval, newest first.
    /// </summary>
    /// <param name="hash">The recipe hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sequence (0 when this hash has no approval event), or the read's error.</returns>
    public async ValueTask<Result<long>> LatestApproval(RecipeHash hash, CancellationToken cancellationToken)
    {
        var own = $"{EventTypes.RecipeApproved}:{hash.Value}:after:";
        var current = await ledger.Latest(EventTypes.RecipeApproved, cancellationToken).ConfigureAwait(false);
        while (current.IsSuccess && current.Value is { } at && !at.IdempotencyKey.StartsWith(own, StringComparison.Ordinal))
        {
            current = await Previous(at, cancellationToken).ConfigureAwait(false);
        }

        return current.IsError ? current.Error : current.Value?.Sequence ?? 0L;
    }

    /// <summary>The approval event <paramref name="at"/> follows (named by its ':after:&lt;seq&gt;' key), or null for the first.</summary>
    private async ValueTask<Result<LedgerEvent?>> Previous(LedgerEvent at, CancellationToken cancellationToken)
    {
        const string After = ":after:";
        var cut = at.IdempotencyKey.LastIndexOf(After, StringComparison.Ordinal);
        if (cut < 0 || !long.TryParse(at.IdempotencyKey.AsSpan(cut + After.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var before) || before <= 0)
        {
            return Results.Success<LedgerEvent?>(value: null);
        }

        var read = await ledger.ReadAfter(before - 1, 1, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return read.Value is [{ } only] && only.Sequence == before
            ? Results.Success<LedgerEvent?>(only)
            : Results.Success<LedgerEvent?>(value: null);
    }

    /// <summary>Appends 'recipe.revoked' for <paramref name="revocation"/>, with the base it cascaded from; revoking the same hash again records nothing.</summary>
    /// <param name="revocation">The stored revocation row.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The event's position (AlreadyRecorded on a repeat), or SND-LEDGER-002 when no lease is held.</returns>
    public ValueTask<Result<LedgerAppend>> Revoked(RecipeRevocation revocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revocation);
        return Append(EventTypes.RecipeRevoked, new RecipeRevoked(revocation.Id, revocation.Version, revocation.Hash, revocation.CascadedFrom),
            $"{EventTypes.RecipeRevoked}:{revocation.Hash.Value}", cancellationToken);
    }

    private static RecipeApproved Payload(RecipeApproval approval, bool drift, ApprovalBatch? batch) =>
        new(approval.Id, approval.Version, approval.Hash, approval.Via, approval.Base, approval.Pins, drift, batch?.Batch, batch?.PlanDigest, batch?.HeldMs);

    /// <summary>Whether <paramref name="previous"/> already records <paramref name="asked"/>.</summary>
    private bool Repeats(LedgerEvent previous, RecipeApproved asked) =>
        events.Payload<RecipeApproved>(previous) is { IsSuccess: true } read && Same(read.Value, asked);

    private static Error NoLease(string type) =>
        SindriErrors.LedgerUnavailable.With($"no lease is held, so '{type}' can't be recorded (fix: restart sindri)");

    /// <summary>Whether <paramref name="recorded"/> is the same approval as <paramref name="asked"/>, pins compared item by item.</summary>
    private static bool Same(RecipeApproved recorded, RecipeApproved asked) =>
        recorded.Id == asked.Id && recorded.Version == asked.Version && recorded.Hash == asked.Hash && recorded.Via == asked.Via
        && recorded.Base == asked.Base && recorded.Drift == asked.Drift
        && recorded.Pins.Blocks.SequenceEqual(asked.Pins.Blocks) && recorded.Pins.Children.SequenceEqual(asked.Pins.Children);

    private async ValueTask<Result<LedgerAppend>> Append<TPayload>(string type, TPayload payload, string key, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return NoLease(type);
        }

        return await ledger.Append(events.Entry(type, payload, key), grant, cancellationToken).ConfigureAwait(false);
    }
}
