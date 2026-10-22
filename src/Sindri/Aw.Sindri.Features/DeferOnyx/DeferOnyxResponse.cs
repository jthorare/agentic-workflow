namespace Aw.Sindri.Features.DeferOnyx;

/// <summary>What 'onyx.deferred' recorded.</summary>
/// <param name="Deferred">The choice now in force.</param>
/// <param name="Sequence">The ledger sequence of 'onyx.deferred' (a read that sends it as X-Sindri-After sees the change).</param>
public sealed record DeferOnyxResponse(bool Deferred, long Sequence);
