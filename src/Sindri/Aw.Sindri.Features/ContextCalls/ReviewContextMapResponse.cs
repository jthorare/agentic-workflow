using Aw.Context.Core.Review;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a review action did.</summary>
/// <param name="Result">The rows written and what is still undecided.</param>
/// <param name="Sequence">The 'context.map.reviewed' event's ledger sequence, or null when no row was written.</param>
public sealed record ReviewContextMapResponse(ReviewResult Result, long? Sequence);
