using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>One question an arm asks the planner: the brief as text, over the map and sources the arm allows.</summary>
/// <param name="Scope">The caller's scope (the planner gates and scrubs with it).</param>
/// <param name="Query">The question.</param>
/// <param name="Map">The map the arm reads, or null for the live current map (the ceiling arm).</param>
/// <param name="Sources">The only sources the question may search; empty for the graph arms.</param>
/// <param name="AsOf">The time every source is asked to cut to: the issue's creation.</param>
/// <param name="Ranker">The ranker stages the arm forces (the ranker arms), or null for the profile's.</param>
public sealed record ArmQuestion(ContextScope Scope, ContextQuery Query, VisibleMap? Map, IReadOnlyList<IContextSource> Sources, DateTimeOffset AsOf, RankerMode? Ranker = null);
