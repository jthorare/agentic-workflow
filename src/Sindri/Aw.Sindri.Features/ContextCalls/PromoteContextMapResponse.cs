using Aw.Context.Core.Map;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a promote did.</summary>
/// <param name="Draft">The draft, as answers name it ('v14@0a1b2c3').</param>
/// <param name="Promoted">The new current version (and the one it retired).</param>
/// <param name="Sequence">The 'context.map.promoted' event's ledger sequence.</param>
public sealed record PromoteContextMapResponse(string Draft, MapPromoted Promoted, long Sequence);
