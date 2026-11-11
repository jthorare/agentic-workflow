using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>One option of a <see cref="NeedsYouDecision"/> (issue #2789).</summary>
/// <param name="Id">The option.</param>
/// <param name="Label">The label.</param>
/// <param name="Cli">The command line that picks it.</param>
/// <param name="RejectLike">True when the option declines or rejects.</param>
public sealed record NeedsYouDecisionOption(DecisionOption Id, string Label, string Cli, bool RejectLike);
