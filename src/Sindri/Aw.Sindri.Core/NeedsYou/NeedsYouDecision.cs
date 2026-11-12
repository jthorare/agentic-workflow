using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>The decision a 'decision' row asks (issue #2789): the pending decision as GET /sindri/decisions?pending=true serves it, so the panel needs no second read.</summary>
/// <param name="Id">The decision's id.</param>
/// <param name="Question">The question, as the decision list words it.</param>
/// <param name="FiledBy">Who filed the request behind it.</param>
/// <param name="Recommended">The option the kind recommends.</param>
/// <param name="Options">The options, each with its command line.</param>
public sealed record NeedsYouDecision(DecisionId Id, string Question, string FiledBy, DecisionOption Recommended, IReadOnlyList<NeedsYouDecisionOption> Options);
