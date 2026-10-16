namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// The ledger event types of an effect's rows (engine spec §5.6, §5.7; engine P6). Effects are ledger events, not
/// Kernel's 'outbox_commands': 'outbox list' folds these rows by effect identity, the latest row of an emission
/// winning. 'outbox.refused' is the spec's name; 'outbox.pending' and 'outbox.dispatched' are this contract's.
/// </summary>
public static class OutboxEvents
{
    /// <summary>The effect was emitted: written after the transition's ledger event, before the dispatch delay.</summary>
    public const string Pending = "outbox.pending";

    /// <summary>The effect's activity performed it. A recipe effect's reply event names this row as its causation.</summary>
    public const string Dispatched = "outbox.dispatched";

    /// <summary>The recheck refused it; the instance parks with spec-conflict of that kind (§3.8), or absorbs it.</summary>
    public const string Refused = "outbox.refused";

    /// <summary>A cancel of the instance aborted the effect within the dispatch delay; no recheck ran and nothing was performed.</summary>
    public const string Cancelled = "outbox.cancelled";

    /// <summary>The event type of a row in <paramref name="state"/>.</summary>
    /// <param name="state">The row's state.</param>
    /// <returns>Its event type.</returns>
    public static string Of(EffectState state) => state switch
    {
        EffectState.Pending => Pending,
        EffectState.Dispatched => Dispatched,
        EffectState.Cancelled => Cancelled,
        EffectState.Refused or _ => Refused,
    };
}
