using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>The fused items after the ranker stages.</summary>
/// <param name="Items">The items, best first.</param>
/// <param name="Degraded">The rerank's row when it was on and the model could not order the items (the items then stay in their feature or fusion order), else null.</param>
internal sealed record RankedItems(IReadOnlyList<FusedItem> Items, DegradedSource? Degraded);
