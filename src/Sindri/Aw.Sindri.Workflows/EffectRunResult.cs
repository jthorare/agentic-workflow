namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'RunEffect' did (engine spec §5.7; engine P6 unit (a)).</summary>
/// <param name="Dispatched">The ledger sequence of the effect's 'outbox.dispatched' row.</param>
/// <param name="Reply">The outcome of the reply event it appended (caused by that row), or null when it replies nothing.</param>
/// <param name="Existed">The effect identity was already performed: nothing was done twice.</param>
internal sealed record EffectRunResult(long Dispatched, string? Reply, bool Existed);
