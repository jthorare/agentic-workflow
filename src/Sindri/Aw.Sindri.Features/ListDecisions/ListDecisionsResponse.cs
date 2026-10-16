using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListDecisions;

/// <summary>The decision list (API spec §3.1).</summary>
/// <param name="Decisions">The decisions: pending oldest first, decided newest first (at most 200).</param>
/// <param name="Empty">The EmptyText sentence for the filters when there is none (or the views are stale), else null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record ListDecisionsResponse(IReadOnlyList<DecisionView> Decisions, string? Empty, AsOf AsOf);
