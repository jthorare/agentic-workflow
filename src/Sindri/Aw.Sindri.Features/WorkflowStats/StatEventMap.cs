using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Stats;

namespace Aw.Sindri.Features.WorkflowStats;

/// <summary>Maps a workflow ledger row to the plain event the recipe stats count, so the counting (in Core) depends on no payload type.</summary>
internal static class StatEventMap
{
    private const string KeyPrefix = "workflow:";

    /// <summary>The instance address in a workflow row's idempotency key ('workflow:{address}:{run}:{activity}'), or null when the key is not one.</summary>
    /// <param name="key">The idempotency key.</param>
    /// <returns>The address.</returns>
    public static string? AddressOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var end = key.IndexOf(':', KeyPrefix.Length);
        return end > KeyPrefix.Length ? key[KeyPrefix.Length..end] : null;
    }

    /// <summary>The stats event for a row of <paramref name="address"/>.</summary>
    /// <param name="address">The instance address.</param>
    /// <param name="at">When the ledger recorded the row.</param>
    /// <param name="payload">The row's workflow payload.</param>
    /// <returns>The event.</returns>
    public static StatEvent Of(string address, DateTimeOffset at, WorkflowEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var answered = payload.Detail?.Answer is not null;
        StatGate[] gates = [.. (payload.Detail?.Gates ?? []).Select(static g => new StatGate(g.Gate.Value, g.Outcome, g.Threshold))];
        if (payload.Detail?.Delta is not { } delta)
        {
            return new StatEvent(address, at, payload.From, payload.To, RecipeId: null, RecipeHash: null, HasDetail: false, Suspension: null, ParkReason: null, Escalation: null, answered, Outcome: null, TokensSpent: null, gates);
        }

        var suspension = delta.Suspension;
        return new StatEvent(
            address,
            at,
            payload.From,
            payload.To,
            RecipeOf(delta.Recipe),
            delta.RecipeHash,
            HasDetail: true,
            suspension?.Kind,
            suspension?.Reason,
            suspension?.Escalation,
            answered,
            delta.Outcome,
            delta.TokensSpent,
            gates);
    }

    private static RecipeId? RecipeOf(string? recipe) => recipe is not null && RecipeId.TryFrom(recipe, out var id) ? id : null;
}
